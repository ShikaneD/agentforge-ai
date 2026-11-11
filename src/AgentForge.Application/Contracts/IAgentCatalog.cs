using AgentForge.Domain.Models;

namespace AgentForge.Application.Contracts;

public interface IAgentCatalog
{
    Task<IReadOnlyList<Agent>> GetAgentsAsync(CancellationToken cancellationToken);
    Task<Agent?> GetAgentAsync(Guid id, CancellationToken cancellationToken);
    Task<Agent> CreateAgentAsync(Agent agent, CancellationToken cancellationToken);
    IReadOnlyList<ModelProviderInfo> GetSupportedProviders();
}
