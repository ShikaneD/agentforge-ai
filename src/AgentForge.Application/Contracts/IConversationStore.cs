using AgentForge.Domain.Models;

namespace AgentForge.Application.Contracts;

public interface IConversationStore
{
    Task<IReadOnlyList<Conversation>> GetConversationsAsync(CancellationToken cancellationToken);
    Task<Conversation?> GetConversationAsync(Guid id, CancellationToken cancellationToken);
    Task<Conversation> AddConversationAsync(string title, Guid? agentId, string firstMessage, CancellationToken cancellationToken);
    Task<Conversation> AddMessageAsync(Guid conversationId, string role, string content, CancellationToken cancellationToken);
}
