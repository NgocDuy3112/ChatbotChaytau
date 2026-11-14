using System.Text.Json;

namespace aida.Services;

public class SystemOneRouter(HttpClient http, IConfiguration config, ILogger<SystemOneRouter> logger)
{
    private readonly string _apiKey = config["OpenRouter:ApiKey"] ?? throw new InvalidOperationException("Missing configuration: OpenRouter:ApiKey");

    private readonly string _systemOneEndpoint = config["OpenRouter:SystemOneEndpoint"] ?? throw new InvalidOperationException("Missing configuration: OpenRouter:SystemOneEndpoint");

    private readonly string _systemOneModel = config["OpenRouter:SystemOneModel"] ?? throw new InvalidOperationException("Missing configuration: OpenRouter:SystemOneModel");
    private readonly string _strongModel = config["OpenRouter:StrongModel"] ?? throw new InvalidOperationException("Missing configuration: OpenRouter:StrongModel");
    private readonly string _moderateModel = config["OpenRouter:ModerateModel"] ?? throw new InvalidOperationException("Missing configuration: OpenRouter:ModerateModel");
    private readonly string _lightModel = config["OpenRouter:LightModel"] ?? throw new InvalidOperationException("Missing configuration: OpenRouter:LightModel");

    public async Task<string> PickChatModelAsync(string userMessage, CancellationToken ct = default)
    {
        var body = new
        {
            model = _systemOneModel,
            state = userMessage,
            questions = new Dictionary<string, object>
            {
                ["tier"] = new
                {
                    type = "choice",
                    instructions = "Which model tier best fits this request?",
                    criteria = new Dictionary<string, string>
                    {
                        ["light"] = "Simple Q&A, short answers, no reasoning.",
                        ["moderate"] = "Moderate tasks, some explanation.",
                        ["strong"] = "Deep reasoning, multi-step analysis."
                    }
                }
            }
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, _systemOneEndpoint);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            req.Content = JsonContent.Create(body);

            using var res = await http.SendAsync(req, ct);

            if (!res.IsSuccessStatusCode)
            {
                var detail = await res.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException(
                    $"System One returned {(int)res.StatusCode}: {detail}");
            }

            using var doc = await JsonDocument.ParseAsync(
                await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

            var choice = doc.RootElement
                .GetProperty("answers").GetProperty("tier").GetProperty("choice").GetString() ?? "light";

            return choice switch
            {
                "strong" => _strongModel,
                "moderate" => _moderateModel,
                _ => _lightModel
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Model routing failed; falling back to the light tier.");
            return _lightModel;
        }
    }

}