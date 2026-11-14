using System.Text;
using aida.Services.Documents;
using aida.Services.Usage;
using Microsoft.Extensions.AI;
using OpenAI;

namespace aida.Services.Agents;

/// <summary>
/// parallel_research: the harness's fan-out tool. Runs up to
/// <see cref="MaxWorkers"/> INDEPENDENT sub-tasks concurrently, each in an
/// isolated context (system + task only — no conversation history), each with
/// its own read_document tool and its own usage reporting row
/// (Step = "worker: …"). One retry per task; a failing task degrades to a
/// marked failure string instead of killing the batch. Cancellation aborts
/// the whole fan-out.
/// </summary>
public class ParallelResearchTool(
    OpenAIClient openAi,
    IUsageTracker tracker,
    PricingTable pricing,
    IWebHostEnvironment env)
{
    internal const int MaxWorkers = 3;

    private const string WorkerInstructions =
        "You are a focused research sub-agent. Complete ONLY the task you are given. "
        + "Use the read_document tool when the task refers to a stored file. Report findings "
        + "concisely as bullet points, and state plainly when information is missing. "
        + "Do not ask questions and do not address the original user directly.";

    public AIFunction Create(string userId, string model) =>
        AIFunctionFactory.Create(
            (string[] tasks, CancellationToken ct) => RunAsync(tasks, userId, model, ct),
            "parallel_research",
            $"Run up to {MaxWorkers} INDEPENDENT research sub-tasks concurrently in isolated "
            + "contexts (each worker can read documents; no conversation history is shared). "
            + "Use only when the tasks do not depend on each other — sequential or dependent "
            + "steps should be done directly. Returns the merged findings of every task.",
            null);

    internal async Task<string> RunAsync(
        string[]? tasks, string userId, string model, CancellationToken ct)
    {
        var cleaned = (tasks ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        if (cleaned.Count == 0)
            return $"Error: parallel_research needs 1–{MaxWorkers} task descriptions.";

        var truncated = cleaned.Count > MaxWorkers;
        var selected = cleaned.Take(MaxWorkers).ToList();

        var results = await Task.WhenAll(
            selected.Select(t => RunWithRetryAsync(t, userId, model, ct)));

        var builder = new StringBuilder();
        if (truncated)
            builder.AppendLine($"(only the first {MaxWorkers} of {cleaned.Count} tasks were run)")
                   .AppendLine();

        for (var i = 0; i < results.Length; i++)
        {
            builder.AppendLine($"=== sub-task {i + 1}/{results.Length}: {selected[i]} ===");
            builder.AppendLine(results[i]);
            if (i < results.Length - 1) builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private async Task<string> RunWithRetryAsync(
        string task, string userId, string model, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                return await RunWorkerAsync(task, userId, model, ct);
            }
            catch (OperationCanceledException)
            {
                throw; // user aborts must abort the fan-out, not be retried
            }
            catch (Exception ex) when (attempt == 2)
            {
                return $"[sub-task failed after {attempt} attempts: {ex.Message}]";
            }
            catch
            {
                // transient failure → single retry
            }
        }

        return "[sub-task failed]"; // unreachable: attempt 2 always returns or throws
    }

    /// <summary>One isolated worker call. Virtual so tests can fake execution.</summary>
    internal virtual async Task<string> RunWorkerAsync(
        string task, string userId, string model, CancellationToken ct)
    {
        // Isolated context: system instructions + this task ONLY. The reporting
        // wrapper logs this call as its own UsageEvent (Step = "worker: …").
        var client = new UsageReportingChatClient(
            openAi.GetChatClient(model).AsIChatClient(),
            tracker, pricing, userId, model, "agent",
            stepLabel: $"worker: {Truncate(task, 48)}");

        var response = await client.GetResponseAsync(
        [
            new ChatMessage(ChatRole.System, WorkerInstructions),
            new ChatMessage(ChatRole.User, task),
        ],
        new ChatOptions
        {
            Tools = [ChatDocumentTools.CreateReadDocument(userId, env)],
        },
        ct);

        var text = response.Text?.Trim();
        return string.IsNullOrEmpty(text) ? "(worker returned no text)" : text;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
