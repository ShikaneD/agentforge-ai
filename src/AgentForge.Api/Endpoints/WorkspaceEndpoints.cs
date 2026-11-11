using AgentForge.Api.Filters;
using AgentForge.Application.Models;
using AgentForge.Application.Services;
using AgentForge.Domain.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentForge.Api.Endpoints;

public static class WorkspaceEndpoints
{
    public static void MapWorkspace(this RouteGroupBuilder api)
    {
        api.MapGet("/providers", (AgentOrchestrator orchestrator) =>
            TypedResults.Ok(orchestrator.GetSupportedProviders()))
            .WithName("GetModelProviders")
            .WithSummary("List supported model providers")
            .WithOpenApi();

        api.MapGet("/conversations", async Task<Ok<IReadOnlyList<ConversationResponse>>> (
            AgentOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            return TypedResults.Ok(await orchestrator.GetConversationsAsync(cancellationToken));
        })
        .WithName("GetConversations")
        .WithSummary("List recent conversations")
        .WithOpenApi();

        api.MapGet("/conversations/{id:guid}", async Task<Results<Ok<ConversationResponse>, NotFound>> (
            Guid id,
            AgentOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            var conversation = await orchestrator.GetConversationAsync(id, cancellationToken);
            return conversation is null ? TypedResults.NotFound() : TypedResults.Ok(conversation);
        })
        .WithName("GetConversation")
        .WithSummary("Get a conversation and its persisted messages")
        .WithOpenApi();

        api.MapPost("/conversations", async Task<Created<ConversationResponse>> (
            ConversationRequest request,
            AgentOrchestrator orchestrator,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var response = await orchestrator.StartConversationAsync(
                request,
                httpContext.User.Identity?.Name ?? "development",
                cancellationToken);
            return TypedResults.Created($"/conversations/{response.Id}", response);
        })
        .AddEndpointFilter<ValidationFilter>()
        .WithName("CreateConversation")
        .WithSummary("Start a conversation")
        .WithOpenApi();
    }
}
