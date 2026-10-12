namespace HouseholdApi.Services;

public record ChatMessage(int Id, string Sender, string Text, DateTime Timestamp);

public static class ConversationIds
{
    public static readonly Guid LegacyDefault = Guid.Parse("00000000-0000-0000-0000-000000000001");
}

public interface IMessageStore
{
    Task<int> AddAsync(Guid conversationId, string sender, string text, CancellationToken ct);
    Task<List<ChatMessage>> ReadAsync(Guid conversationId, int count, CancellationToken ct);
}

public interface IAiService
{
    /// <param name="history">Recent messages from one conversation, oldest first. The newest one is the user's current message.</param>
    Task<string> GetReplyAsync(IReadOnlyList<ChatMessage> history, CancellationToken ct);
}

/// <summary>Fallback used until an AI provider is configured.</summary>
public class PlaceholderAiService : IAiService
{
    public Task<string> GetReplyAsync(IReadOnlyList<ChatMessage> history, CancellationToken ct)
    {
        var last = history.LastOrDefault(m => m.Sender == "user")?.Text ?? "";
        return Task.FromResult($"Placeholder reply: you said \"{last}\". The AI model isn't connected yet.");
    }
}
