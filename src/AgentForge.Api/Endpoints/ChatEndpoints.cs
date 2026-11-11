using AgentForge.Api.Filters;
using AgentForge.Application.Models;
using AgentForge.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentForge.Api.Endpoints;

public static class ChatEndpoints
{
    public static void MapChat(this RouteGroupBuilder api)
    {
        var chat = api.MapPost("/chat", async Task<Results<Created<ChatResponse>, Ok<ChatResponse>>> (
            ChatRequest request,
            ChatService chatService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var response = await chatService.SendAsync(
                request,
                httpContext.User.Identity?.Name ?? "development",
                cancellationToken);
            return request.ConversationId is null
                ? TypedResults.Created($"/conversations/{response.ConversationId}", response)
                : TypedResults.Ok(response);
        })
        .AddEndpointFilter<ValidationFilter>()
        .WithName("SendChatMessage")
        .WithSummary("Send a message to an agent and receive a model response")
        .WithOpenApi();

        chat.RequireRateLimiting("chat");
    }
}
