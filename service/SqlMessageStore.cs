using Dapper;
using Microsoft.Data.SqlClient;

namespace HouseholdApi.Services;

/// <summary>SQL Server backed message storage.</summary>
public class SqlMessageStore(IConfiguration configuration) : IMessageStore
{
    private readonly string _connectionString = configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default is missing.");

    private SqlConnection Open() => new(_connectionString);

    public async Task EnsureSchemaAsync()
    {
        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "schema.sql"));
        await using var db = Open();
        await db.ExecuteAsync(sql);
    }

    public async Task<int> AddAsync(Guid conversationId, string sender, string text, CancellationToken ct)
    {
        await using var db = Open();
        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            "INSERT INTO dbo.Messages (ConversationId, Sender, [Text]) OUTPUT INSERTED.Id VALUES (@conversationId, @sender, @text)",
            new { conversationId, sender, text }, cancellationToken: ct));
    }

    /// <summary>Latest messages for a conversation, returned oldest first.</summary>
    public async Task<List<ChatMessage>> ReadAsync(Guid conversationId, int count, CancellationToken ct)
    {
        await using var db = Open();
        var rows = await db.QueryAsync<ChatMessage>(new CommandDefinition(
            "SELECT Id, Sender, [Text] AS Text, CreatedAt AS Timestamp FROM (SELECT TOP (@count) * FROM dbo.Messages WHERE ConversationId = @conversationId ORDER BY Id DESC) AS recent ORDER BY Id ASC",
            new { conversationId, count }, cancellationToken: ct));

        return rows.Select(row => row with
        {
            Timestamp = DateTime.SpecifyKind(row.Timestamp, DateTimeKind.Utc)
        }).ToList();
    }
}
