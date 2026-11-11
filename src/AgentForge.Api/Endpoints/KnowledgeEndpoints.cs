using AgentForge.Api.Filters;
using AgentForge.Application.Contracts;
using AgentForge.Application.Models;

namespace AgentForge.Api.Endpoints;

public static class KnowledgeEndpoints
{
    public static void MapKnowledge(this RouteGroupBuilder api, bool enforceAdminRole)
    {
        var ingest = api.MapPost("/knowledge/documents", async (
            IngestDocumentRequest request,
            IKnowledgeStore knowledgeStore,
            IAuditLogger auditLogger,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var document = await knowledgeStore.AddDocumentAsync(request.Name, request.Content, cancellationToken);
            await auditLogger.WriteAsync(
                httpContext.User.Identity?.Name ?? "development",
                "knowledge.ingest",
                "document",
                document.Id.ToString(),
                cancellationToken);
            return Results.Created(
                $"/knowledge/documents/{document.Id}",
                new KnowledgeDocumentResponse(document.Id, document.Name, document.Chunks.Count, document.CreatedAtUtc));
        })
        .AddEndpointFilter<ValidationFilter>()
        .WithName("IngestKnowledgeDocument")
        .WithSummary("Ingest a plain-text document into the knowledge store")
        .WithOpenApi();

        if (enforceAdminRole)
        {
            ingest.RequireAuthorization(policy => policy.RequireRole("Admin"));
        }

        api.MapGet("/knowledge/search", async (
            string query,
            IKnowledgeStore knowledgeStore,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length > 500)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["query"] = ["A query between 1 and 500 characters is required."]
                });
            }

            var chunks = await knowledgeStore.SearchAsync(query, 10, cancellationToken);
            return Results.Ok(chunks.Select(chunk => new
            {
                source = chunk.Document?.Name ?? "Knowledge source",
                chunk.Content
            }));
        })
        .WithName("SearchKnowledge")
        .WithSummary("Search ingested knowledge documents")
        .WithOpenApi();
    }
}
