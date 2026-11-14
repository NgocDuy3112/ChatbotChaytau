using System.Text.Json;
using aida.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace aida.Services.Conversations;

public class ConversationStore(
    IServiceScopeFactory scopeFactory,
    IDistributedCache cache) : IConversationStore
{
    /// <summary>Short-lived so the sidebar stays snappy across the per-message
    /// reloads without ever showing stale titles for long.</summary>
    private static readonly TimeSpan ListCacheTtl = TimeSpan.FromSeconds(60);

    private static string ListKey(string userId) => $"aida:conversations:{userId}";
    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<IReadOnlyList<ConversationSummary>> ListAsync(string userId, CancellationToken ct = default)
    {
        var key = ListKey(userId);
        var cached = await cache.GetStringAsync(key, ct);
        if (cached is not null)
            return JsonSerializer.Deserialize<List<ConversationSummary>>(cached) ?? [];

        var list = await WithDbAsync<IReadOnlyList<ConversationSummary>>(async db =>
            await db.Conversations
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.UpdatedAt)
                .Select(c => new ConversationSummary(c.Id, c.Title, c.UpdatedAt))
                .ToListAsync(ct), ct);

        await cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(list),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ListCacheTtl },
            ct);

        return list;
    }

    public async Task<ConversationSummary> CreateAsync(string userId, CancellationToken ct = default)
    {
        var summary = await WithDbAsync(async db =>
        {
            var c = new Conversation { UserId = userId };
            db.Conversations.Add(c);
            await db.SaveChangesAsync(ct);
            return new ConversationSummary(c.Id, c.Title, c.UpdatedAt);
        }, ct);

        await InvalidateListAsync(userId, ct);
        return summary;
    }

    public Task<IReadOnlyList<ChatMessageRecord>> LoadMessagesAsync(Guid conversationId, string userId, CancellationToken ct = default) =>
        WithDbAsync<IReadOnlyList<ChatMessageRecord>>(async db =>
            await db.ChatMessages
                .Where(m => m.ConversationId == conversationId && m.Conversation!.UserId == userId)
                .OrderBy(m => m.Ordinal)
                .Select(m => new ChatMessageRecord(m.Role, m.Text))
                .ToListAsync(ct), ct);

    public async Task AppendAsync(Guid conversationId, string userId, string role, string text, CancellationToken ct = default)
    {
        await WithDbAsync(async db =>
        {
            var conversation = await db.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId, ct)
                ?? throw new InvalidOperationException("Conversation not found.");

            var next = await db.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .Select(m => (int?)m.Ordinal).MaxAsync(ct) ?? 0;

            db.ChatMessages.Add(new ChatMessageRow
            {
                ConversationId = conversationId,
                Role = role,
                Text = text,
                Ordinal = next + 1
            });
            conversation.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);

        await InvalidateListAsync(userId, ct);
    }

    public async Task RenameAsync(Guid conversationId, string userId, string title, CancellationToken ct = default)
    {
        await WithDbAsync(async db =>
        {
            await db.Conversations
                .Where(c => c.Id == conversationId && c.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Title, title), ct);
            return true;
        }, ct);

        await InvalidateListAsync(userId, ct);
    }

    public async Task DeleteAsync(Guid conversationId, string userId, CancellationToken ct = default)
    {
        await WithDbAsync(async db =>
        {
            await db.Conversations
                .Where(c => c.Id == conversationId && c.UserId == userId)
                .ExecuteDeleteAsync(ct);
            return true;
        }, ct);

        await InvalidateListAsync(userId, ct);
    }

    private async Task WithDbAsync(Func<AppDbContext, Task> work, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task InvalidateListAsync(string userId, CancellationToken ct) =>
        cache.RemoveAsync(ListKey(userId), ct);
}