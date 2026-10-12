namespace HouseholdApi.Services;

/// <summary>Temporary process-local message storage. Messages are lost when the application restarts.</summary>
public class MessageStore : IMessageStore
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, List<ChatMessage>> _conversations = [];
    private int _nextId = 1;

    public Task<int> AddAsync(Guid conversationId, string sender, string text, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_lock)
        {
            if (!_conversations.TryGetValue(conversationId, out var messages))
                _conversations[conversationId] = messages = [];
            var id = _nextId++;
            messages.Add(new ChatMessage(id, sender, text, DateTime.UtcNow));
            return Task.FromResult(id);
        }
    }

    /// <summary>Latest messages for a conversation, returned oldest first.</summary>
    public Task<List<ChatMessage>> ReadAsync(Guid conversationId, int count, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_lock)
        {
            var messages = _conversations.TryGetValue(conversationId, out var stored)
                ? stored.TakeLast(count).ToList()
                : [];
            return Task.FromResult(messages);
        }
    }
}
