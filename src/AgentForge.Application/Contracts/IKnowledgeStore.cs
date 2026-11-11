using AgentForge.Domain.Models;

namespace AgentForge.Application.Contracts;

public interface IKnowledgeStore
{
    Task<KnowledgeDocument> AddDocumentAsync(string name, string content, CancellationToken cancellationToken);
    Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(string query, int limit, CancellationToken cancellationToken);
}
