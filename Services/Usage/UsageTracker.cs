using aida.Data;

namespace aida.Services.Usage;

/// <summary>Records one usage event per LLM call. Fire-and-forget safe.</summary>
public interface IUsageTracker
{
    /// <summary>Persists the event. Never throws — usage bookkeeping must not
    /// break a chat response.</summary>
    Task RecordAsync(UsageEvent evt, CancellationToken ct = default);
}

internal sealed class UsageTracker(
    IServiceScopeFactory scopeFactory,
    ILogger<UsageTracker> logger) : IUsageTracker
{
    public async Task RecordAsync(UsageEvent evt, CancellationToken ct = default)
    {
        try
        {
            if (evt.Error is { Length: > 500 })
                evt.Error = evt.Error[..500];

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UsageEvents.Add(evt);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Swallowed on purpose: callers fire-and-forget, and a metrics
            // failure must never surface in (or crash) the user's chat.
            logger.LogWarning(ex, "Could not record usage event for {Model}.", evt.Model);
        }
    }
}
