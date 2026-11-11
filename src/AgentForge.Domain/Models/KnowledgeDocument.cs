namespace AgentForge.Domain.Models;

public sealed class KnowledgeDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<KnowledgeChunk> Chunks { get; set; } = new();
}

public sealed class KnowledgeChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public int Position { get; set; }
    public string Content { get; set; } = string.Empty;
    public KnowledgeDocument? Document { get; set; }
}
