using System.Text.Json;

namespace AgentForge.Application.Contracts;

public sealed record McpRemoteTool(string Server, string Name, string Description, JsonElement InputSchema);

public interface IMcpRemoteClient
{
    Task<IReadOnlyList<McpRemoteTool>> ListToolsAsync(CancellationToken cancellationToken);
    Task<JsonElement> CallToolAsync(string serverName, string toolName, JsonElement arguments, CancellationToken cancellationToken);
}
