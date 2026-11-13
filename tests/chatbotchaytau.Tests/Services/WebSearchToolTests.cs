using chatbotchaytau.Services.WebSearch;
using Microsoft.Extensions.Configuration;

namespace chatbotchaytau.Tests.Services;

public class WebSearchToolTests
{
    private static WebSearchTool Tool(params (string Key, string Value)[] settings) =>
        new(new HttpClient(), new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value))
            .Build());

    // --- tool registration gating ------------------------------------------

    [Fact]
    public void Create_returns_null_without_an_api_key()
    {
        Assert.Null(Tool().Create());
        Assert.Null(Tool(("WebSearch:ApiKey", "")).Create());
        Assert.Null(Tool(("WebSearch:ApiKey", "   ")).Create());
    }

    [Fact]
    public void Create_registers_the_tool_when_a_key_is_configured()
    {
        var function = Tool(("WebSearch:ApiKey", "tvly-test-key")).Create();

        Assert.NotNull(function);
        Assert.Equal("web_search", function!.Name);
    }

    // --- response formatting (pure) ----------------------------------------

    [Fact]
    public void FormatResponse_formats_answer_and_results()
    {
        const string json = """
        {
          "answer": "Paris is the capital of France.",
          "results": [
            { "title": "Paris - Wikipedia", "url": "https://en.wikipedia.org/wiki/Paris",
              "content": "Paris is the capital and largest city of France.", "score": 0.98 },
            { "title": "Facts about Paris", "url": "https://example.com/paris",
              "content": "Paris has been the capital since 987 AD.", "score": 0.91 }
          ]
        }
        """;

        var text = WebSearchTool.FormatResponse(json);

        Assert.StartsWith("Answer: Paris is the capital of France.", text);
        Assert.Contains("[1] Paris - Wikipedia", text);
        Assert.Contains("https://en.wikipedia.org/wiki/Paris", text);
        Assert.Contains("capital and largest city", text);
        Assert.Contains("[2] Facts about Paris", text);
    }

    [Fact]
    public void FormatResponse_caps_results_at_five()
    {
        var items = string.Join(",", Enumerable.Range(1, 7).Select(i =>
            $$"""{ "title": "T{{i}}", "url": "https://u/{{i}}", "content": "c{{i}}" }"""));
        var json = $$"""{ "results": [{{items}}] }""";

        var text = WebSearchTool.FormatResponse(json);

        Assert.Contains("[5]", text);
        Assert.DoesNotContain("[6]", text);
        Assert.DoesNotContain("[7]", text);
    }

    [Fact]
    public void FormatResponse_missing_results_field_says_so()
    {
        Assert.Equal("Search returned no results field.", WebSearchTool.FormatResponse("""{ "oops": 1 }"""));
    }

    [Fact]
    public void FormatResponse_empty_results_says_so()
    {
        Assert.Equal("Search returned no results.", WebSearchTool.FormatResponse("""{ "results": [] }"""));
    }

    [Fact]
    public void FormatResponse_keeps_the_answer_even_without_results()
    {
        var text = WebSearchTool.FormatResponse("""{ "answer": "42" }""");

        Assert.Equal("Answer: 42", text);
    }

    [Fact]
    public void FormatResponse_survives_result_entries_with_missing_fields()
    {
        var text = WebSearchTool.FormatResponse("""{ "results": [ {}, { "title": "only title" } ] }""");

        Assert.Contains("[1]", text);
        Assert.Contains("[2] only title", text);
    }
}
