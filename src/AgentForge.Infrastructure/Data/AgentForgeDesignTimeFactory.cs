using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AgentForge.Infrastructure.Data;

public sealed class AgentForgeDesignTimeFactory : IDesignTimeDbContextFactory<AgentForgeDbContext>
{
    public AgentForgeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AgentForgeDbContext>()
            .UseSqlServer("Server=localhost;Database=AgentForge;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False")
            .Options;

        return new AgentForgeDbContext(options);
    }
}
