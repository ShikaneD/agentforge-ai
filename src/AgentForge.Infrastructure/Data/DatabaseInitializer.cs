using AgentForge.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Infrastructure.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AgentForgeDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        if (await dbContext.Agents.AnyAsync(cancellationToken))
        {
            return;
        }

        dbContext.Agents.AddRange(
            new Agent
            {
                Name = "Knowledge Agent",
                Description = "Retrieves relevant context from ingested knowledge sources.",
                Role = AgentRole.Knowledge,
                ModelProvider = "Gemini"
            },
            new Agent
            {
                Name = "Developer Agent",
                Description = "Analyses code and supports engineering tasks.",
                Role = AgentRole.Developer,
                ModelProvider = "Ollama"
            },
            new Agent
            {
                Name = "Analytics Agent",
                Description = "Analyses approved data and produces summaries.",
                Role = AgentRole.Analytics,
                ModelProvider = "Gemini"
            },
            new Agent
            {
                Name = "Validation Agent",
                Description = "Reviews generated responses for completeness and consistency.",
                Role = AgentRole.Validation,
                ModelProvider = "OpenAI"
            });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
