using System.Diagnostics;
using aida.Data;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace aida.Services.Usage;

/// <summary>
/// IChatClient middleware that records one <see cref="UsageEvent"/> per
/// underlying model call. Wrapped underneath the Agent Framework harness, a
/// multi-call agent turn (plan → tool rounds → synthesis) becomes per-call
/// admin rows whose <see cref="UsageEvent.Step"/> says what preceded the call
/// ("plan", "after read_document", …). Ask mode never wraps this — Chat.razor
/// logs exactly one row per send there.
/// </summary>
internal sealed class UsageReportingChatClient : DelegatingChatClient
{
    private readonly IUsageTracker _tracker;
    private readonly PricingTable _pricing;
    private readonly string _userId;
    private readonly string _model;
    private readonly string _mode;
    private readonly string? _stepLabel;

    public UsageReportingChatClient(
        IChatClient innerClient,
        IUsageTracker tracker,
        PricingTable pricing,
        string userId,
        string model,
        string mode,
        string? stepLabel = null)
        : base(innerClient)
    {
        _tracker = tracker;
        _pricing = pricing;
        _userId = userId;
        _model = model;
        _mode = mode;
        _stepLabel = stepLabel;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        var clock = Stopwatch.StartNew();

        var response = await base.GetResponseAsync(request, options, cancellationToken);
        clock.Stop();

        var functionCalls = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .Count();

        Record(response.Usage, functionCalls, clock.ElapsedMilliseconds,
            _stepLabel ?? StepFor(request), error: null);

        return response;
    }

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => RecordStreamAsync(messages, options, cancellationToken);

    private async IAsyncEnumerable<ChatResponseUpdate> RecordStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        var step = _stepLabel ?? StepFor(request);
        var clock = Stopwatch.StartNew();
        UsageDetails? usage = null;
        var functionCalls = 0;
        string? error = null;

        var enumerator = base
            .GetStreamingResponseAsync(request, options, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        try
        {
            while (true)
            {
                ChatResponseUpdate update;
                try
                {
                    if (!await enumerator.MoveNextAsync()) break;
                    update = enumerator.Current;
                }
                catch (OperationCanceledException)
                {
                    error = "cancelled";
                    throw;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    throw;
                }

                foreach (var content in update.Contents)
                {
                    if (content is UsageContent reported) usage = reported.Details;
                    if (content is FunctionCallContent) functionCalls++;
                }

                // Legal: the enclosing try has only a finally (yield is banned
                // in try blocks that have catch clauses).
                yield return update;
            }
        }
        finally
        {
            clock.Stop();
            await enumerator.DisposeAsync();
            Record(usage, functionCalls, clock.ElapsedMilliseconds, step, error);
        }
    }

    /// <summary>What preceded this call: "plan" for the first call of a turn,
    /// otherwise the most recent tool the agent executed.</summary>
    private static string StepFor(IReadOnlyList<ChatMessage> messages)
    {
        string? lastTool = null;
        foreach (var message in messages)
        {
            foreach (var content in message.Contents)
            {
                if (content is FunctionCallContent call)
                    lastTool = call.Name;
            }
        }

        return lastTool is null ? "plan" : $"after {lastTool}";
    }

    private void Record(
        UsageDetails? usage, int functionCalls, long durationMs, string step, string? error)
    {
        _ = _tracker.RecordAsync(new UsageEvent
        {
            UserId = _userId,
            Model = _model,
            Mode = _mode,
            PromptTokens = usage?.InputTokenCount,
            CompletionTokens = usage?.OutputTokenCount,
            CostUsd = _pricing.CostUsd(_model, usage?.InputTokenCount, usage?.OutputTokenCount),
            DurationMs = durationMs,
            ToolCalls = functionCalls,
            Step = step,
            Error = error,
        });
    }
}
