namespace chatbotchaytau.Data;

/// <summary>
/// One LLM call: what it cost, how long it took, whether it worked.
/// Day 3 fills <see cref="Step"/> with the agent sub-call label — agent runs
/// log one row per call so multi-agent spend stays visible.
/// </summary>
public class UsageEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Identity user id of the caller.</summary>
    public string UserId { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public string Model { get; set; } = string.Empty;

    /// <summary>"ask" | "agent" (extend as modes are added).</summary>
    public string Mode { get; set; } = string.Empty;

    public long? PromptTokens { get; set; }
    public long? CompletionTokens { get; set; }

    /// <summary>USD at call time. Null = pricing unknown for the model or the
    /// provider didn't report usage — never fabricated, the admin page counts
    /// these as "unpriced".</summary>
    public decimal? CostUsd { get; set; }

    /// <summary>Pipeline duration in milliseconds (retrieval + LLM stream).</summary>
    public long DurationMs { get; set; }

    /// <summary>Function calls executed during this call (tool usage intensity).</summary>
    public int ToolCalls { get; set; }

    /// <summary>Agent sub-call label (Day 3); null for plain sends.</summary>
    public string? Step { get; set; }

    /// <summary>null = success · "cancelled" = user aborted · other = error text.</summary>
    public string? Error { get; set; }
}
