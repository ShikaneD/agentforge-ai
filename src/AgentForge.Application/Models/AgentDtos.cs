using AgentForge.Domain.Models;
using System.ComponentModel.DataAnnotations;

namespace AgentForge.Application.Models;

/// <summary>Payload for creating a configured agent.</summary>
public sealed record CreateAgentRequest
{
    [Required, MaxLength(100)]
    public required string Name { get; init; }

    [Required, MaxLength(500)]
    public required string Description { get; init; }

    public required AgentRole Role { get; init; }

    [Required, MaxLength(30)]
    public required string ModelProvider { get; init; }

    public bool IsEnabled { get; init; } = true;
}

/// <summary>Agent configuration returned by the API.</summary>
public sealed record AgentResponse(
    Guid Id,
    string Name,
    string Description,
    AgentRole Role,
    string ModelProvider,
    bool IsEnabled,
    DateTimeOffset CreatedAtUtc);

/// <summary>Payload for starting a conversation.</summary>
public sealed record ConversationRequest
{
    public Guid? AgentId { get; init; }

    [MaxLength(120)]
    public string Title { get; init; } = string.Empty;

    [Required, MaxLength(12000)]
    public required string Message { get; init; }
}

/// <summary>Payload for sending a message to an agent.</summary>
public sealed record ChatRequest
{
    public Guid? ConversationId { get; init; }

    public Guid? AgentId { get; init; }

    [Required, MinLength(1), MaxLength(12000)]
    public required string Message { get; init; }
}

/// <summary>Message returned as part of a conversation.</summary>
public sealed record ConversationMessageResponse(
    Guid Id,
    string Role,
    string Content,
    DateTimeOffset CreatedAtUtc);

/// <summary>Conversation and its persisted messages.</summary>
public sealed record ConversationResponse(
    Guid Id,
    Guid? AgentId,
    string Title,
    IReadOnlyList<ConversationMessageResponse> Messages,
    DateTimeOffset UpdatedAtUtc);

/// <summary>Result of a chat turn.</summary>
public sealed record ChatResponse(
    Guid ConversationId,
    Guid AgentId,
    string AgentName,
    string AssistantMessage,
    IReadOnlyList<string> Sources,
    DateTimeOffset CreatedAtUtc);

/// <summary>Payload for adding a plain-text knowledge document.</summary>
public sealed record IngestDocumentRequest
{
    [Required, MaxLength(200)]
    public required string Name { get; init; }

    [Required, MinLength(1), MaxLength(1000000)]
    public required string Content { get; init; }
}

/// <summary>Summary of an ingested knowledge document.</summary>
public sealed record KnowledgeDocumentResponse(Guid Id, string Name, int ChunkCount, DateTimeOffset CreatedAtUtc);

/// <summary>Input for an agent-to-agent task.</summary>
public sealed record AgentTaskRequest
{
    [Required, MaxLength(12000)]
    public required string Task { get; init; }

    public Guid? AgentId { get; init; }
}
