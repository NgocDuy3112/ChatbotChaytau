using System.Collections.Concurrent;
using StackExchange.Redis;

namespace chatbotchaytau.Services.RateLimit;

/// <summary>Admission control for chat sends — protects OpenRouter spend.</summary>
public interface IChatRateLimiter
{
    /// <summary>True when this send may proceed (inside the per-minute budget).</summary>
    Task<bool> TryAcquireAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// Fixed one-minute window per user. Uses Redis INCR when a multiplexer is
/// provided (shared across instances, survives restarts); otherwise an
/// in-memory dictionary keeps local dev protected too. Fails OPEN — if Redis
/// has an issue, chat keeps working and the failure is logged.
/// A limit of 0 or less disables the limiter entirely.
/// </summary>
internal class ChatRateLimiter : IChatRateLimiter
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly int _limit;
    private readonly ILogger<ChatRateLimiter> _logger;

    private readonly ConcurrentDictionary<string, (long Count, DateTimeOffset StartsAt)> _memory = new();

    public ChatRateLimiter(
        IConnectionMultiplexer? redis,
        IConfiguration configuration,
        ILogger<ChatRateLimiter> logger)
    {
        _redis = redis;
        _limit = configuration.GetValue("RateLimit:ChatPerMinute", 20);
        _logger = logger;
    }

    /// <summary>Overridable clock so window rollover is testable without waits.</summary>
    internal virtual DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public async Task<bool> TryAcquireAsync(string userId, CancellationToken ct = default)
    {
        if (_limit <= 0) return true;

        return _redis is null
            ? TryAcquireInMemory(userId)
            : await TryAcquireInRedisAsync(userId, ct);
    }

    private bool TryAcquireInMemory(string userId)
    {
        var now = UtcNow;
        var entry = _memory.AddOrUpdate(
            userId,
            _ => (1L, now),
            (_, existing) => now - existing.StartsAt >= TimeSpan.FromMinutes(1)
                ? (1L, now)
                : (existing.Count + 1, existing.StartsAt));

        return entry.Count <= _limit;
    }

    private async Task<bool> TryAcquireInRedisAsync(string userId, CancellationToken ct)
    {
        try
        {
            var db = _redis!.GetDatabase();
            // Minute-bucket key: the window rolls over by key name — no cleanup needed.
            var key = $"chatbotchaytau:rl:chat:{userId}:{UtcNow:yyyyMMddHHmm}";
            var count = await db.StringIncrementAsync(key);
            if (count == 1)
                await db.KeyExpireAsync(key, TimeSpan.FromSeconds(70));
            return count <= _limit;
        }
        catch (Exception ex)
        {
            // Fail open: availability over strict enforcement; a broken Redis
            // must not take the chat down. Logged so it doesn't stay silent.
            _logger.LogWarning(ex, "Redis rate-limit check failed; allowing the send.");
            return true;
        }
    }
}
