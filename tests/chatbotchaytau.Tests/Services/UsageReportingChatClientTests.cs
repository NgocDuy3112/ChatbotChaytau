using System.Runtime.CompilerServices;
using chatbotchaytau.Data;
using System.Text;
using chatbotchaytau.Services.Usage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace chatbotchaytau.Tests.Services;

public class UsageReportingChatClientTests
{
    // --- fakes -------------------------------------------------------------

    private sealed class RecordingTracker : IUsageTracker
    {
        public List<UsageEvent> Events { get; } = [];

        public Task RecordAsync(UsageEvent evt, CancellationToken ct = default)
        {
            Events.Add(evt);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeChatClient : IChatClient
    {
        public ChatResponse? NextResponse { get; init; }
        public List<ChatResponseUpdate> StreamUpdates { get; } = [];
        public Exception? ThrowOnStream { get; init; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (NextResponse is null) throw new InvalidOperationException("no canned response");
            return Task.FromResult(NextResponse);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ThrowOnStream is not null) throw ThrowOnStream;

            foreach (var update in StreamUpdates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    // --- helpers -----------------------------------------------------------

    private static readonly (string Key, string Value)[] PriceConfig =
    [
        ("OpenRouter:Pricing:openai/gpt-4o:In", "2.5"),
        ("OpenRouter:Pricing:openai/gpt-4o:Out", "10"),
    ];

    private static (UsageReportingChatClient Client, RecordingTracker Tracker) Wrap(FakeChatClient inner)
    {
        var pricing = new PricingTable(new ConfigurationBuilder()
            .AddInMemoryCollection(PriceConfig.ToDictionary(p => p.Key, p => (string?)p.Value))
            .Build());

        var tracker = new RecordingTracker();
        var client = new UsageReportingChatClient(inner, tracker, pricing,
            userId: "user-1", model: "openai/gpt-4o", mode: "agent");
        return (client, tracker);
    }

    private static ChatResponse ResponseWithUsage(long prompt, long completion,
        params AIContent[] contents)
    {
        var response = new ChatResponse(
        [
            new ChatMessage(ChatRole.Assistant, contents.ToList()),
        ])
        {
            Usage = new UsageDetails
            {
                InputTokenCount = prompt,
                OutputTokenCount = completion,
            },
        };
        return response;
    }

    // --- tests -------------------------------------------------------------

    [Fact]
    public async Task Non_streaming_call_records_usage_cost_and_tool_count()
    {
        var inner = new FakeChatClient
        {
            NextResponse = ResponseWithUsage(1000, 500,
                new FunctionCallContent("c1", "save_document", new Dictionary<string, object?>())),
        };
        var (client, tracker) = Wrap(inner);

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        var evt = Assert.Single(tracker.Events);
        Assert.Equal("user-1", evt.UserId);
        Assert.Equal("openai/gpt-4o", evt.Model);
        Assert.Equal("agent", evt.Mode);
        Assert.Equal(1000, evt.PromptTokens);
        Assert.Equal(500, evt.CompletionTokens);
        Assert.Equal(0.0075m, evt.CostUsd);   // 1000×$2.5 + 500×$10 per 1M
        Assert.Equal(1, evt.ToolCalls);
        Assert.Equal("plan", evt.Step);       // no prior tool calls in the request
        Assert.Null(evt.Error);
    }

    [Fact]
    public async Task Streaming_records_exactly_one_event_with_stream_usage()
    {
        var inner = new FakeChatClient();
        inner.StreamUpdates.Add(new ChatResponseUpdate { Contents = [new TextContent("P")] });
        inner.StreamUpdates.Add(new ChatResponseUpdate
        {
            Contents = [new UsageContent(new UsageDetails
            {
                InputTokenCount = 700,
                OutputTokenCount = 300,
            })],
        });
        var (client, tracker) = Wrap(inner);

        var text = new StringBuilder();
        await foreach (var update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")]))
        {
            text.Append(update.Text);
        }

        var evt = Assert.Single(tracker.Events);
        Assert.Equal(700, evt.PromptTokens);
        Assert.Equal(300, evt.CompletionTokens);
        Assert.Equal(0.00475m, evt.CostUsd);    // 700×$2.5 + 300×$10 per 1M
        Assert.Equal("plan", evt.Step);
        Assert.Null(evt.Error);
        Assert.Equal("P", text.ToString());
    }

    [Fact]
    public async Task Step_label_describes_the_last_tool_the_agent_ran()
    {
        var inner = new FakeChatClient
        {
            NextResponse = ResponseWithUsage(100, 50),
        };
        var (client, tracker) = Wrap(inner);

        // Request history contains an earlier tool call + its result — this
        // model call happens BECAUSE of read_document.
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "read the cv"),
            new(ChatRole.Assistant, [new FunctionCallContent("c1", "read_document", new Dictionary<string, object?>())]),
            new(ChatRole.Tool, [new FunctionResultContent("c1", "cv contents…")]),
        };

        await client.GetResponseAsync(history);

        Assert.Equal("after read_document", Assert.Single(tracker.Events).Step);
    }

    [Fact]
    public async Task Missing_usage_still_records_a_row_with_null_tokens()
    {
        var inner = new FakeChatClient();
        inner.StreamUpdates.Add(new ChatResponseUpdate { Contents = [new TextContent("plain")] });
        var (client, tracker) = Wrap(inner);

        await foreach (var _ in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")]))
        {
        }

        var evt = Assert.Single(tracker.Events);
        Assert.Null(evt.PromptTokens);
        Assert.Null(evt.CostUsd);
        Assert.Null(evt.Error);
    }

    [Fact]
    public async Task Failed_stream_records_the_error_and_rethrows()
    {
        var inner = new FakeChatClient { ThrowOnStream = new HttpRequestException("upstream boom") };
        var (client, tracker) = Wrap(inner);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            DrainAsync(client));

        var evt = Assert.Single(tracker.Events);
        Assert.Equal("upstream boom", evt.Error);
        Assert.Null(evt.CostUsd);
    }

    [Fact]
    public async Task Cancelled_stream_records_cancelled_and_propagates()
    {
        var inner = new FakeChatClient();
        inner.StreamUpdates.Add(new ChatResponseUpdate { Contents = [new TextContent("x")] });
        var (client, tracker) = Wrap(inner);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DrainAsync(client, cts.Token));

        var evt = Assert.Single(tracker.Events);
        Assert.Equal("cancelled", evt.Error);
    }

    private static async Task DrainAsync(UsageReportingChatClient client,
        CancellationToken ct = default)
    {
        await foreach (var _ in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")], cancellationToken: ct))
        {
        }
    }
}
