using System.Text.Json;
using System.Text.Json.Serialization;
using AgentForge.Api.Filters;
using AgentForge.Application.Contracts;
using AgentForge.Application.Models;
using AgentForge.Application.Services;

namespace AgentForge.Api.Endpoints;

public static class InteropEndpoints
{
    private static readonly object[] McpTools =
    [
        new
        {
            name = "knowledge.search",
            description = "Search ingested AgentForge documents.",
            inputSchema = new { type = "object", properties = new { query = new { type = "string" } }, required = new[] { "query" } }
        },
        new
        {
            name = "agents.list",
            description = "List enabled AgentForge specialists.",
            inputSchema = new { type = "object", properties = new { } }
        },
        new
        {
            name = "agents.delegate",
            description = "Delegate a bounded task to another enabled AgentForge specialist.",
            inputSchema = new
            {
                type = "object",
                properties = new { agentId = new { type = "string" }, task = new { type = "string" } },
                required = new[] { "agentId", "task" }
            }
        }
    ];

    public static void MapInterop(this RouteGroupBuilder api, bool enforceAdminRole)
    {
        api.MapGet("/.well-known/agent-card.json", (HttpContext context, IConfiguration configuration) =>
        {
            var baseUrl = configuration["A2A:BaseUrl"]?.TrimEnd('/')
                ?? $"{context.Request.Scheme}://{context.Request.Host}";
            return Results.Ok(new
            {
                name = "AgentForge",
                description = "Multi-agent AI workspace with controlled tools and knowledge retrieval.",
                url = baseUrl,
                version = "0.1.0",
                protocolVersion = "1.0",
                capabilities = new { streaming = false, pushNotifications = false },
                skills = new[]
                {
                    new { id = "agent.task", name = "Delegate task", description = "Send a task to a configured AgentForge specialist." }
                },
                authentication = new { schemes = new[] { "Bearer" } }
            });
        })
        .WithName("GetAgentCard")
        .WithSummary("Publish the AgentForge A2A agent card")
        .WithOpenApi();

        api.MapPost("/a2a/tasks", async (
            AgentTaskRequest request,
            ChatService chatService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var response = await chatService.SendAsync(
                new ChatRequest { AgentId = request.AgentId, Message = request.Task },
                httpContext.User.Identity?.Name ?? "a2a-peer",
                cancellationToken);
            return Results.Created($"/conversations/{response.ConversationId}", new
            {
                taskId = response.ConversationId,
                status = "completed",
                agent = new { id = response.AgentId, name = response.AgentName },
                output = response.AssistantMessage,
                sources = response.Sources
            });
        })
        .AddEndpointFilter<ValidationFilter>()
        .WithName("CreateA2ATask")
        .WithSummary("Submit a task to an AgentForge specialist")
        .WithOpenApi();

        api.MapGet("/a2a/agents", async (
            IA2ARemoteClient remoteClient,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await remoteClient.DiscoverAsync(cancellationToken));
        })
        .WithName("DiscoverA2AAgents")
        .WithSummary("Discover configured remote A2A agents")
        .WithOpenApi();

        api.MapPost("/a2a/agents/{agentName}/tasks", async (
            string agentName,
            AgentTaskRequest request,
            IA2ARemoteClient remoteClient,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await remoteClient.SendTaskAsync(agentName, request, cancellationToken));
        })
        .AddEndpointFilter<ValidationFilter>()
        .WithName("SendRemoteA2ATask")
        .WithSummary("Send a task to a configured remote A2A agent")
        .WithOpenApi();

        api.MapGet("/mcp/remote-tools", async (
            IMcpRemoteClient remoteClient,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await remoteClient.ListToolsAsync(cancellationToken));
        })
        .WithName("DiscoverRemoteMcpTools")
        .WithSummary("Discover tools from configured remote MCP servers")
        .WithOpenApi();

        var callRemoteTool = api.MapPost("/mcp/servers/{serverName}/tools/{toolName}", async (
            string serverName,
            string toolName,
            McpRemoteToolCallRequest request,
            IMcpRemoteClient remoteClient,
            IAuditLogger auditLogger,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await remoteClient.CallToolAsync(serverName, toolName, request.Arguments, cancellationToken);
            await auditLogger.WriteAsync(
                httpContext.User.Identity?.Name ?? "development",
                "mcp.remote-tool",
                "tool",
                $"{serverName}/{toolName}",
                cancellationToken);
            return Results.Ok(result);
        })
        .AddEndpointFilter<ValidationFilter>()
        .WithName("CallRemoteMcpTool")
        .WithSummary("Execute an explicitly configured remote MCP tool")
        .WithOpenApi();

        if (enforceAdminRole)
        {
            callRemoteTool.RequireAuthorization(policy => policy.RequireRole("Admin"));
        }

        api.MapPost("/mcp", async (
            McpRpcRequest request,
            IAgentToolExecutor toolExecutor,
            CancellationToken cancellationToken) =>
        {
            if (request.Jsonrpc != "2.0")
            {
                return Results.BadRequest(new { error = "jsonrpc must be 2.0" });
            }

            if (request.Method == "notifications/initialized")
            {
                return Results.NoContent();
            }

            var response = request.Method switch
            {
                "initialize" => new McpRpcResponse(request.Jsonrpc, request.Id, new
                {
                    protocolVersion = "2025-03-26",
                    capabilities = new { tools = new { listChanged = false } },
                    serverInfo = new { name = "AgentForge MCP", version = "0.1.0" }
                }, null),
                "tools/list" => new McpRpcResponse(request.Jsonrpc, request.Id, new { tools = McpTools }, null),
                "tools/call" => await CallToolAsync(request, toolExecutor, cancellationToken),
                _ => new McpRpcResponse(request.Jsonrpc, request.Id, null, new McpError(-32601, "Method not found"))
            };

            return Results.Json(response);
        })
        .AddEndpointFilter<ValidationFilter>()
        .WithName("McpJsonRpc")
        .WithSummary("MCP initialize, tool discovery, and tool execution")
        .WithOpenApi();
    }

    private static async Task<object> CallToolAsync(
        McpRpcRequest request,
        IAgentToolExecutor toolExecutor,
        CancellationToken cancellationToken)
    {
        var parameters = request.Parameters;
        if (!parameters.TryGetProperty("name", out var nameElement)
            || nameElement.ValueKind != JsonValueKind.String
            || !parameters.TryGetProperty("arguments", out var arguments)
            || arguments.ValueKind != JsonValueKind.Object)
        {
            return new McpRpcResponse(request.Jsonrpc, request.Id, null, new McpError(-32602, "Invalid tool call parameters"));
        }

        try
        {
            var result = await toolExecutor.ExecuteAsync(
                nameElement.GetString()!,
                arguments,
                Guid.Empty,
                cancellationToken);
            return new McpRpcResponse(
                request.Jsonrpc,
                request.Id,
                new { content = new[] { new { type = "text", text = result } }, isError = false },
                null);
        }
        catch (ArgumentException)
        {
            return new McpRpcResponse(
                request.Jsonrpc,
                request.Id,
                null,
                new McpError(-32602, "Invalid tool arguments or tool is not allowed"));
        }
        catch (KeyNotFoundException)
        {
            return new McpRpcResponse(
                request.Jsonrpc,
                request.Id,
                null,
                new McpError(-32004, "Requested resource was not found"));
        }
    }

    private sealed record McpRpcRequest(
        string Jsonrpc,
        JsonElement Id,
        string Method,
        [property: JsonPropertyName("params")]
        JsonElement Parameters);

    private sealed record McpRpcResponse(
        string Jsonrpc,
        JsonElement Id,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        object? Result,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        McpError? Error);
    private sealed record McpRemoteToolCallRequest(JsonElement Arguments);
    private sealed record McpError(int Code, string Message);
}
