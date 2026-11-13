namespace chatbotchaytau.Services;

/// <summary>User-selectable chat behavior, switched from the input toolbar.
/// Binary by design: plain Q&A vs. an agent that works with tools.</summary>
public enum ChatMode
{
    /// <summary>Pure Q&A: router picks the model, retrieval on, no document tools.</summary>
    Ask,

    /// <summary>Deep-research shell: forced strong model + document tools.
    /// Full orchestration lands on Day 3 (Microsoft Agent Framework).</summary>
    Agent,
}

internal static class ChatModePolicy
{
    /// <summary>Document tools run only in Agent mode.</summary>
    internal static bool UsesTools(ChatMode mode) => mode == ChatMode.Agent;

    /// <summary>Model this mode forces. Null = let the router decide.
    /// A user pin always wins; Agent falls back to the configured strong model.</summary>
    internal static string? ForcedModel(ChatMode mode, string? pinnedModel, string? strongModel) =>
        pinnedModel ?? (mode == ChatMode.Agent && !string.IsNullOrWhiteSpace(strongModel)
            ? strongModel
            : null);
}
