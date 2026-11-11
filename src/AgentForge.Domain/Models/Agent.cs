namespace AgentForge.Domain.Models;

public enum AgentRole
{
    Knowledge,
    Developer,
    Analytics,
    Validation
}

public sealed class Agent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AgentRole Role { get; set; }
    public string ModelProvider { get; set; } = "Gemini";
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
