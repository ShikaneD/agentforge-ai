using AgentForge.Application.Contracts;
using AgentForge.Application.Models;
using AgentForge.Domain.Models;

namespace AgentForge.Application.Services;

public sealed class AgentOrchestrator
{
    private readonly IAgentCatalog _agentCatalog;
    private readonly IConversationStore _conversationStore;
    private readonly IAuditLogger _auditLogger;

    public AgentOrchestrator(
        IAgentCatalog agentCatalog,
        IConversationStore conversationStore,
        IAuditLogger auditLogger)
    {
        _agentCatalog = agentCatalog;
        _conversationStore = conversationStore;
        _auditLogger = auditLogger;
    }

    public async Task<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken cancellationToken)
    {
        return (await _agentCatalog.GetAgentsAsync(cancellationToken))
            .Select(agent => ToAgentResponse(agent))
            .ToList();
    }

    public async Task<AgentResponse> CreateAgentAsync(
        CreateAgentRequest request,
        string subject,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Role))
        {
            throw new ArgumentException("The selected agent role is not supported.");
        }

        var provider = request.ModelProvider.Trim();
        if (!_agentCatalog.GetSupportedProviders().Any(item => item.Name.Equals(provider, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("The selected model provider is not supported.");
        }

        var agent = new Agent
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Role = request.Role,
            ModelProvider = provider,
            IsEnabled = request.IsEnabled
        };

        var created = await _agentCatalog.CreateAgentAsync(agent, cancellationToken);
        await _auditLogger.WriteAsync(subject, "agent.create", "agent", created.Id.ToString(), cancellationToken);
        return ToAgentResponse(created);
    }

    public IReadOnlyList<ModelProviderInfo> GetSupportedProviders()
    {
        return _agentCatalog.GetSupportedProviders();
    }

    public async Task<IReadOnlyList<ConversationResponse>> GetConversationsAsync(CancellationToken cancellationToken)
    {
        return (await _conversationStore.GetConversationsAsync(cancellationToken))
            .Select(ToConversationResponse)
            .ToList();
    }

    public async Task<ConversationResponse?> GetConversationAsync(Guid id, CancellationToken cancellationToken)
    {
        var conversation = await _conversationStore.GetConversationAsync(id, cancellationToken);
        return conversation is null ? null : ToConversationResponse(conversation);
    }

    public async Task<ConversationResponse> StartConversationAsync(
        ConversationRequest request,
        string subject,
        CancellationToken cancellationToken)
    {
        if (request.AgentId is { } agentId)
        {
            var agent = await _agentCatalog.GetAgentAsync(agentId, cancellationToken)
                ?? throw new KeyNotFoundException("The selected agent was not found.");
            if (!agent.IsEnabled)
            {
                throw new ArgumentException("The selected agent is disabled.");
            }
        }

        var conversation = await _conversationStore.AddConversationAsync(
            request.Title,
            request.AgentId,
            request.Message,
            cancellationToken);
        await _auditLogger.WriteAsync(subject, "conversation.create", "conversation", conversation.Id.ToString(), cancellationToken);
        return ToConversationResponse(conversation);
    }

    private static AgentResponse ToAgentResponse(Agent agent)
    {
        return new AgentResponse(
            agent.Id,
            agent.Name,
            agent.Description,
            agent.Role,
            agent.ModelProvider,
            agent.IsEnabled,
            agent.CreatedAtUtc);
    }

    private static ConversationResponse ToConversationResponse(Conversation conversation)
    {
        return new ConversationResponse(
            conversation.Id,
            conversation.AgentId,
            conversation.Title,
            conversation.Messages
                .Select(message => new ConversationMessageResponse(
                    message.Id,
                    message.Role,
                    message.Content,
                    message.CreatedAtUtc))
                .ToList(),
            conversation.UpdatedAtUtc);
    }
}
