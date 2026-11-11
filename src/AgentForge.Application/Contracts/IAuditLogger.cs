namespace AgentForge.Application.Contracts;

public interface IAuditLogger
{
    Task WriteAsync(string subject, string action, string resourceType, string resourceId, CancellationToken cancellationToken);
}
