using aida.Data;

namespace aida.Services.Conversations;

public record ChatMessageRecord(string Role, string Text);

public record ConversationSummary(Guid Id, string Title, DateTimeOffset UpdatedAt);

public interface IConversationStore
{
    Task<IReadOnlyList<ConversationSummary>> ListAsync(string userId, CancellationToken ct = default);
    Task<ConversationSummary> CreateAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessageRecord>> LoadMessagesAsync(Guid conversationId, string userId, CancellationToken ct = default);
    Task AppendAsync(Guid conversationId, string userId, string role, string text, CancellationToken ct = default);
    Task RenameAsync(Guid conversationId, string userId, string title, CancellationToken ct = default);
    Task DeleteAsync(Guid conversationId, string userId, CancellationToken ct = default);
}