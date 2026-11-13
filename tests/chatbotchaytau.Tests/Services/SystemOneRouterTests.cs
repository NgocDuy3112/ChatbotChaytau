using System.Net;
using System.Text;
using System.Text.Json;
using chatbotchaytau.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace chatbotchaytau.Tests.Services;

public class SystemOneRouterTests
{
    private const string Endpoint = "https://router.test/v1/systemone";

    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenRouter:ApiKey"] = "test-key",
            ["OpenRouter:SystemOneEndpoint"] = Endpoint,
            ["OpenRouter:SystemOneModel"] = "typesafe/jev-test",
            ["OpenRouter:StrongModel"] = "openai/strong",
            ["OpenRouter:ModerateModel"] = "openai/moderate",
            ["OpenRouter:LightModel"] = "openai/light",
        })
        .Build();

    private static (SystemOneRouter Router, StubHandler Handler) CreateRouter(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var router = new SystemOneRouter(
            new HttpClient(handler), Config(), NullLogger<SystemOneRouter>.Instance);
        return (router, handler);
    }

    private static HttpResponseMessage Json(string json,
        HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Choice(string choice) =>
        Json($"{{\"answers\":{{\"tier\":{{\"choice\":\"{choice}\"}}}}}}");

    // --- tier mapping -----------------------------------------------------

    [Theory]
    [InlineData("strong", "openai/strong")]
    [InlineData("moderate", "openai/moderate")]
    [InlineData("light", "openai/light")]
    public async Task Valid_choice_maps_to_configured_model(string choice, string expected)
    {
        var (router, _) = CreateRouter(_ => Choice(choice));

        var model = await router.PickChatModelAsync("hello");

        Assert.Equal(expected, model);
    }

    [Fact]
    public async Task Unknown_choice_falls_back_to_light()
    {
        var (router, _) = CreateRouter(_ => Choice("turbo"));

        Assert.Equal("openai/light", await router.PickChatModelAsync("hello"));
    }

    // --- fail-safe fallback ------------------------------------------------

    [Fact]
    public async Task Server_error_falls_back_to_light()
    {
        var (router, _) = CreateRouter(_ =>
            Json("boom", HttpStatusCode.InternalServerError));

        Assert.Equal("openai/light", await router.PickChatModelAsync("hello"));
    }

    [Fact]
    public async Task Malformed_json_falls_back_to_light()
    {
        var (router, _) = CreateRouter(_ => Json("{not json"));

        Assert.Equal("openai/light", await router.PickChatModelAsync("hello"));
    }

    [Fact]
    public async Task Missing_answers_property_falls_back_to_light()
    {
        var (router, _) = CreateRouter(_ => Json("{\"unrelated\":1}"));

        Assert.Equal("openai/light", await router.PickChatModelAsync("hello"));
    }

    [Fact]
    public async Task Network_failure_falls_back_to_light()
    {
        var (router, _) = CreateRouter(_ => throw new HttpRequestException("no route"));

        Assert.Equal("openai/light", await router.PickChatModelAsync("hello"));
    }

    [Fact]
    public async Task Cancellation_is_not_swallowed_by_the_fallback()
    {
        var (router, _) = CreateRouter(_ => throw new InvalidOperationException(
            "should not be reached when the token is already cancelled"));

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // The fail-safe must not turn a user-initiated cancel into a light-tier call.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => router.PickChatModelAsync("hello", cts.Token));
    }

    // --- request contract --------------------------------------------------

    [Fact]
    public async Task Request_carries_bearer_token_and_system_one_shape()
    {
        var (router, handler) = CreateRouter(_ => Choice("moderate"));

        await router.PickChatModelAsync("What is 2+2?");

        Assert.Equal(Endpoint, handler.LastRequestUri?.ToString());
        Assert.Equal("Bearer test-key", handler.LastAuthorization);

        using var doc = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("typesafe/jev-test", doc.RootElement.GetProperty("model").GetString());
        // state is the raw message string (current contract).
        Assert.Equal("What is 2+2?", doc.RootElement.GetProperty("state").GetString());
        Assert.Equal("choice",
            doc.RootElement.GetProperty("questions").GetProperty("tier")
                .GetProperty("type").GetString());
    }

    // --- stub ---------------------------------------------------------------

    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }
        public string? LastAuthorization { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastRequestUri = request.RequestUri;
            LastAuthorization = request.Headers.Authorization?.ToString();
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return respond(request);
        }
    }
}
