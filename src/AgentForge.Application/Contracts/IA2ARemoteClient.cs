using AgentForge.Application.Models;
using System.Text.Json;

namespace AgentForge.Application.Contracts;

public sealed record A2ARemoteAgent(string Name, string Url, JsonElement Card);

public interface IA2ARemoteClient
{
    Task<IReadOnlyList<A2ARemoteAgent>> DiscoverAsync(CancellationToken cancellationToken);
    Task<JsonElement> SendTaskAsync(string agentName, AgentTaskRequest request, CancellationToken cancellationToken);
}
