namespace AgentForge.Domain.Models;

public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? AgentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<ConversationMessage> Messages { get; set; } = new();
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ConversationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
