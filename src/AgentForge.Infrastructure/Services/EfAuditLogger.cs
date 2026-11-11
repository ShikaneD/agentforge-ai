using AgentForge.Application.Contracts;
using AgentForge.Domain.Models;
using AgentForge.Infrastructure.Data;

namespace AgentForge.Infrastructure.Services;

public sealed class EfAuditLogger(AgentForgeDbContext dbContext) : IAuditLogger
{
    public async Task WriteAsync(
        string subject,
        string action,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken)
    {
        dbContext.AuditEntries.Add(new AuditEntry
        {
            Subject = string.IsNullOrWhiteSpace(subject) ? "unknown" : subject[..Math.Min(subject.Length, 200)],
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
