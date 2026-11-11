using AgentForge.Application.Contracts;
using AgentForge.Domain.Models;
using AgentForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Infrastructure.Services;

public sealed class EfConversationStore(AgentForgeDbContext dbContext) : IConversationStore
{
    public async Task<IReadOnlyList<Conversation>> GetConversationsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Conversations.AsNoTracking()
            .OrderByDescending(conversation => conversation.UpdatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    public Task<Conversation?> GetConversationAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Conversations
            .Include(conversation => conversation.Messages.OrderBy(message => message.CreatedAtUtc))
            .FirstOrDefaultAsync(conversation => conversation.Id == id, cancellationToken);
    }

    public async Task<Conversation> AddConversationAsync(
        string title,
        Guid? agentId,
        string firstMessage,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var conversationId = Guid.NewGuid();
        var conversation = new Conversation
        {
            Id = conversationId,
            AgentId = agentId,
            Title = string.IsNullOrWhiteSpace(title) ? firstMessage[..Math.Min(firstMessage.Length, 80)] : title.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Messages = [new ConversationMessage
            {
                ConversationId = conversationId,
                Role = "user",
                Content = firstMessage,
                CreatedAtUtc = now
            }]
        };

        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    public async Task<Conversation> AddMessageAsync(
        Guid conversationId,
        string role,
        string content,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .Include(item => item.Messages)
            .FirstOrDefaultAsync(item => item.Id == conversationId, cancellationToken)
            ?? throw new KeyNotFoundException("The requested conversation was not found.");

        conversation.Messages.Add(new ConversationMessage
        {
            ConversationId = conversationId,
            Role = role,
            Content = content,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        conversation.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return conversation;
    }
}
