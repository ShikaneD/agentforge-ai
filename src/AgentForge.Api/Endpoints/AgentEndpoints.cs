using AgentForge.Api.Filters;
using AgentForge.Application.Models;
using AgentForge.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentForge.Api.Endpoints;

public static class AgentEndpoints
{
    public static void MapAgents(this RouteGroupBuilder api, bool enforceAdminRole)
    {
        api.MapGet("/agents", async Task<Ok<IReadOnlyList<AgentResponse>>> (
            AgentOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            return TypedResults.Ok(await orchestrator.GetAgentsAsync(cancellationToken));
        })
        .WithName("GetAgents")
        .WithSummary("List configured agents")
        .WithOpenApi();

        var create = api.MapPost("/agents", async Task<Created<AgentResponse>> (
            CreateAgentRequest request,
            AgentOrchestrator orchestrator,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var subject = httpContext.User.Identity?.Name ?? "development";
            var response = await orchestrator.CreateAgentAsync(request, subject, cancellationToken);
            return TypedResults.Created($"/agents/{response.Id}", response);
        })
        .AddEndpointFilter<ValidationFilter>()
        .WithName("CreateAgent")
        .WithSummary("Create an agent")
        .WithOpenApi();

        if (enforceAdminRole)
        {
            create.RequireAuthorization(policy => policy.RequireRole("Admin"));
        }
    }
}
