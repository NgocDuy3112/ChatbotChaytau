using System.Net.Http.Headers;
using Microsoft.Extensions.AI;
using System.Text;
using System.Text.Json;

namespace chatbotchaytau.Services.WebSearch;

/// <summary>
/// Tavily-backed web_search tool for the agent harness. Registers ONLY when
/// <c>WebSearch:ApiKey</c> is configured — without a key the harness simply
/// never sees the tool (document-only research keeps working).
/// </summary>
public sealed class WebSearchTool(HttpClient http, IConfiguration config)
{
    internal const string Endpoint = "https://api.tavily.com/search";

    /// <summary>null when no API key is configured (tool disabled).</summary>
    public AIFunction? Create()
    {
        var apiKey = config["WebSearch:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        return AIFunctionFactory.Create(
            (string query, CancellationToken ct) => SearchAsync(query, apiKey, ct),
            "web_search",
            "Search the public web for current external information. Returns up to 5 results, "
            + "each with title, URL and a content excerpt, plus a short answer when available. "
            + "Use for facts that may have changed or are not in the user's documents.",
            null);
    }

    internal async Task<string> SearchAsync(string query, string apiKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "Error: web_search needs a non-empty query.";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = JsonContent.Create(new
            {
                query,
                max_results = 5,
                include_answer = false,
                search_depth = "basic",
            });

            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            return response.IsSuccessStatusCode
                ? FormatResponse(body)
                : $"Search failed ({(int)response.StatusCode}): {Truncate(body, 300)}";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return $"Search failed: {ex.Message}";
        }
    }

    /// <summary>Tavily response → compact text for the model. Pure and testable.</summary>
    internal static string FormatResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var builder = new StringBuilder();

        if (root.TryGetProperty("answer", out var answer)
            && answer.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(answer.GetString()))
        {
            builder.Append("Answer: ").Append(answer.GetString()).Append("\n\n");
        }

        if (!root.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array)
        {
            return builder.Length > 0
                ? builder.ToString().Trim()
                : "Search returned no results field.";
        }

        var index = 0;
        foreach (var result in results.EnumerateArray())
        {
            if (++index > 5) break;

            builder.Append('[').Append(index).Append("] ")
                   .Append(String(result, "title")).Append('\n')
                   .Append(String(result, "url")).Append('\n')
                   .Append(String(result, "content")).Append("\n\n");
        }

        return builder.Length == 0 ? "Search returned no results." : builder.ToString().Trim();

        static string String(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}
