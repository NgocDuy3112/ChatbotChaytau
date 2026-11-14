using aida.Services.RateLimit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace aida.Tests.Services;

public class ChatRateLimiterTests
{
    /// <summary>In-memory limiter (no Redis) with a manually advanced clock
    /// so window rollover is tested without sleeping.</summary>
    private sealed class TestLimiter(int chatPerMinute) : ChatRateLimiter(
        redis: null,
        configuration: new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:ChatPerMinute"] = chatPerMinute.ToString(),
            })
            .Build(),
        logger: NullLogger<ChatRateLimiter>.Instance)
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

        internal override DateTimeOffset UtcNow => Now;
    }

    [Fact]
    public async Task Allows_up_to_the_limit_then_rejects()
    {
        var limiter = new TestLimiter(3);

        Assert.True(await limiter.TryAcquireAsync("user-1"));
        Assert.True(await limiter.TryAcquireAsync("user-1"));
        Assert.True(await limiter.TryAcquireAsync("user-1"));
        Assert.False(await limiter.TryAcquireAsync("user-1"));
    }

    [Fact]
    public async Task Limit_is_per_user()
    {
        var limiter = new TestLimiter(1);

        Assert.True(await limiter.TryAcquireAsync("user-1"));
        Assert.False(await limiter.TryAcquireAsync("user-1"));
        Assert.True(await limiter.TryAcquireAsync("user-2"));
    }

    [Fact]
    public async Task Window_rollover_restores_the_budget()
    {
        var limiter = new TestLimiter(1);

        Assert.True(await limiter.TryAcquireAsync("user-1"));
        Assert.False(await limiter.TryAcquireAsync("user-1"));

        limiter.Now = limiter.Now.AddMinutes(1);
        Assert.True(await limiter.TryAcquireAsync("user-1"));
    }

    [Fact]
    public async Task Non_positive_limit_disables_the_limiter()
    {
        var limiter = new TestLimiter(0);

        for (var i = 0; i < 50; i++)
            Assert.True(await limiter.TryAcquireAsync("user-1"));
    }
}
