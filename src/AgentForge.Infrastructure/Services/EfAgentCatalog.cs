using AgentForge.Application.Contracts;
using AgentForge.Domain.Models;
using AgentForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Infrastructure.Services;

public sealed class EfAgentCatalog(AgentForgeDbContext dbContext) : IAgentCatalog
{
    private static readonly IReadOnlyList<ModelProviderInfo> Providers =
    [
        new() { Name = "Gemini", DisplayName = "Google Gemini", Description = "Hosted Google Gemini models.", RequiresApiKey = true },
        new() { Name = "OpenAI", DisplayName = "OpenAI", Description = "Hosted OpenAI models.", RequiresApiKey = true },
        new() { Name = "Ollama", DisplayName = "Ollama", Description = "Locally hosted models.", RequiresApiKey = false }
    ];

    public async Task<IReadOnlyList<Agent>> GetAgentsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Agents.AsNoTracking()
            .OrderBy(agent => agent.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Agent?> GetAgentAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Agents.AsNoTracking().FirstOrDefaultAsync(agent => agent.Id == id, cancellationToken);
    }

    public async Task<Agent> CreateAgentAsync(Agent agent, CancellationToken cancellationToken)
    {
        dbContext.Agents.Add(agent);
        await dbContext.SaveChangesAsync(cancellationToken);
        return agent;
    }

    public IReadOnlyList<ModelProviderInfo> GetSupportedProviders() => Providers;
}
