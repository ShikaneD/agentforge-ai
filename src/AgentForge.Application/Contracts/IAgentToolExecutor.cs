using System.Text.Json;

namespace AgentForge.Application.Contracts;

public interface IAgentToolExecutor
{
    Task<string> ExecuteAsync(string name, JsonElement arguments, Guid callingAgentId, CancellationToken cancellationToken);
}
