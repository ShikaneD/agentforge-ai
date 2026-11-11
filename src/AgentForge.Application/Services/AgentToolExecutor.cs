using System.Text.Json;
using AgentForge.Application.Contracts;
using AgentForge.Application.Models;
using AgentForge.Domain.Models;

namespace AgentForge.Application.Services;

public sealed class AgentToolExecutor(
    IAgentCatalog agentCatalog,
    IKnowledgeStore knowledgeStore,
    IModelProviderRegistry providerRegistry,
    IAuditLogger auditLogger) : IAgentToolExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> ExecuteAsync(
        string name,
        JsonElement arguments,
        Guid callingAgentId,
        CancellationToken cancellationToken)
    {
        object result = name switch
        {
            "knowledge.search" => await SearchKnowledgeAsync(arguments, cancellationToken),
            "agents.list" => await ListAgentsAsync(cancellationToken),
            "agents.delegate" => await DelegateAsync(arguments, callingAgentId, cancellationToken),
            _ => throw new ArgumentException($"Tool '{name}' is not allowed.")
        };

        await auditLogger.WriteAsync(
            $"agent:{callingAgentId}",
            $"tool.{name}",
            "tool",
            name,
            cancellationToken);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    private async Task<object> SearchKnowledgeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var query = GetString(arguments, "query", 500);
        var chunks = await knowledgeStore.SearchAsync(query, 5, cancellationToken);
        return chunks.Select(chunk => new
        {
            source = chunk.Document?.Name ?? "Knowledge source",
            content = chunk.Content
        });
    }

    private async Task<object> ListAgentsAsync(CancellationToken cancellationToken)
    {
        var agents = await agentCatalog.GetAgentsAsync(cancellationToken);
        return agents.Where(agent => agent.IsEnabled)
            .Select(agent => new { agent.Id, agent.Name, role = agent.Role.ToString(), agent.Description });
    }

    private async Task<object> DelegateAsync(
        JsonElement arguments,
        Guid callingAgentId,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetProperty("agentId", out var agentIdValue)
            || !agentIdValue.TryGetGuid(out var agentId))
        {
            throw new ArgumentException("The delegation tool requires a valid agentId.");
        }

        if (agentId == callingAgentId)
        {
            throw new ArgumentException("An agent cannot delegate a task to itself.");
        }

        var task = GetString(arguments, "task", 4000);
        var agent = await agentCatalog.GetAgentAsync(agentId, cancellationToken)
            ?? throw new KeyNotFoundException("The delegated agent was not found.");
        if (!agent.IsEnabled)
        {
            throw new ArgumentException("The delegated agent is disabled.");
        }

        var provider = providerRegistry.GetProvider(agent.ModelProvider);
        var response = await provider.CompleteAsync(
            new ModelCompletionRequest(
                provider.DefaultModel,
                $"You are {agent.Name}, a {agent.Role} specialist. Complete the delegated task safely. Return JSON with a string property named 'message'.",
                [new ModelMessage("user", task)],
                true),
            cancellationToken);

        try
        {
            using var document = JsonDocument.Parse(response);
            if (!document.RootElement.TryGetProperty("message", out var message)
                || message.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(message.GetString()))
            {
                throw new InvalidOperationException("The delegated agent returned an invalid response.");
            }

            return new { agentId = agent.Id, agentName = agent.Name, message = message.GetString() };
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The delegated agent returned an invalid structured response.", exception);
        }
    }

    private static string GetString(JsonElement arguments, string propertyName, int maxLength)
    {
        if (!arguments.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException($"Tool argument '{propertyName}' is required.");
        }

        var text = value.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > maxLength)
        {
            throw new ArgumentException($"Tool argument '{propertyName}' is empty or too long.");
        }

        return text;
    }
}
