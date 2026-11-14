using aida.Data;
using Microsoft.EntityFrameworkCore;

namespace aida.Services.Usage;

public sealed record ModelStat(string Model, long Requests, long PromptTokens, long CompletionTokens, decimal Cost);

public sealed record DayStat(DateOnly Day, long Requests, long PromptTokens, decimal Cost);

public sealed record ModeStat(string Mode, long Requests, decimal Cost, long Errors);

public sealed record UserStat(string UserId, string? Email, long Requests, decimal Cost, DateTimeOffset LastSeen);

public sealed record FailureStat(DateTimeOffset Timestamp, string Model, string Mode, string Error);

public sealed record AdminOverview(
    long Requests,
    long PromptTokens,
    long CompletionTokens,
    decimal Cost,
    long DistinctUsers,
    long Errors,
    long UnpricedRequests,
    IReadOnlyList<ModelStat> ByModel,
    IReadOnlyList<DayStat> ByDay,
    IReadOnlyList<ModeStat> ByMode,
    IReadOnlyList<UserStat> TopUsers,
    IReadOnlyList<FailureStat> RecentFailures);

/// <summary>Aggregates usage events for the admin portal.</summary>
public interface IUsageReport
{
    Task<AdminOverview> GetAsync(int days = 14, CancellationToken ct = default);
}

/// <summary>
/// Loads the window's rows once and aggregates in memory — avoids provider
/// translation quirks around date bucketing and keeps the queries trivial.
/// Fine at chat-scale (thousands of rows per fortnight).
/// </summary>
internal sealed class UsageReport(AppDbContext db) : IUsageReport
{
    public async Task<AdminOverview> GetAsync(int days = 14, CancellationToken ct = default)
    {
        var from = DateTimeOffset.UtcNow.AddDays(-days);

        var rows = await db.UsageEvents
            .Where(e => e.Timestamp >= from)
            .Select(e => new
            {
                e.UserId,
                e.Timestamp,
                e.Model,
                e.Mode,
                e.PromptTokens,
                e.CompletionTokens,
                e.CostUsd,
                e.Error,
            })
            .ToListAsync(ct);

        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var emails = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, ct);

        var byModel = rows
            .GroupBy(r => r.Model)
            .Select(g => new ModelStat(
                g.Key,
                g.LongCount(),
                g.Sum(r => r.PromptTokens ?? 0),
                g.Sum(r => r.CompletionTokens ?? 0),
                g.Sum(r => r.CostUsd ?? 0m)))
            .OrderByDescending(m => m.Cost)
            .ThenByDescending(m => m.Requests)
            .ToList();

        var byDay = rows
            .GroupBy(r => DateOnly.FromDateTime(r.Timestamp.UtcDateTime.Date))
            .Select(g => new DayStat(
                g.Key,
                g.LongCount(),
                g.Sum(r => r.PromptTokens ?? 0),
                g.Sum(r => r.CostUsd ?? 0m)))
            .OrderBy(d => d.Day)
            .ToList();

        var byMode = rows
            .GroupBy(r => r.Mode)
            .Select(g => new ModeStat(
                g.Key,
                g.LongCount(),
                g.Sum(r => r.CostUsd ?? 0m),
                g.LongCount(r => r.Error is not null)))
            .OrderByDescending(m => m.Requests)
            .ToList();

        var topUsers = rows
            .GroupBy(r => r.UserId)
            .Select(g => new UserStat(
                g.Key,
                emails.GetValueOrDefault(g.Key),
                g.LongCount(),
                g.Sum(r => r.CostUsd ?? 0m),
                g.Max(r => r.Timestamp)))
            .OrderByDescending(u => u.Cost)
            .ThenByDescending(u => u.Requests)
            .Take(10)
            .ToList();

        var failures = rows
            .Where(r => r.Error is not null)
            .OrderByDescending(r => r.Timestamp)
            .Take(20)
            .Select(r => new FailureStat(r.Timestamp, r.Model, r.Mode, r.Error!))
            .ToList();

        return new AdminOverview(
            Requests: rows.Count,
            PromptTokens: rows.Sum(r => r.PromptTokens ?? 0),
            CompletionTokens: rows.Sum(r => r.CompletionTokens ?? 0),
            Cost: rows.Sum(r => r.CostUsd ?? 0m),
            DistinctUsers: userIds.Count,
            Errors: rows.LongCount(r => r.Error is not null),
            UnpricedRequests: rows.LongCount(r => r.Error is null && r.CostUsd is null),
            ByModel: byModel,
            ByDay: byDay,
            ByMode: byMode,
            TopUsers: topUsers,
            RecentFailures: failures);
    }
}
