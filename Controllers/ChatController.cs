using HouseholdApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdApi.Controllers;

[ApiController]
[Route("api")]
public class ChatController(IMessageStore store, IAiService ai) : ControllerBase
{
    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<ChatMessage>>> GetHistory([FromQuery] Guid conversationId, CancellationToken ct)
    {
        if (conversationId == Guid.Empty)
            return BadRequest(new { error = "A valid conversationId is required." });

        var messages = await store.ReadAsync(conversationId, 200, ct);
        return Ok(messages);
    }

    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request, CancellationToken ct)
    {
        if (request.ConversationId == Guid.Empty)
            return BadRequest(new { error = "A valid conversationId is required." });

        var text = request.Text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 4000)
            return BadRequest(new { error = "Text is required and must be under 4000 characters." });

        await store.AddAsync(request.ConversationId, "user", text, ct);
        var history = await store.ReadAsync(request.ConversationId, 20, ct);
        var reply = await ai.GetReplyAsync(history, ct);
        await store.AddAsync(request.ConversationId, "bot", reply, ct);

        return Ok(new ChatResponse(reply));
    }
}

public record ChatRequest(Guid ConversationId, string? Text);
public record ChatResponse(string Reply);
