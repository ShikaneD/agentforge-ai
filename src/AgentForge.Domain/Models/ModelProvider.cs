namespace AgentForge.Domain.Models;

public sealed class ModelProviderInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresApiKey { get; set; }
}
