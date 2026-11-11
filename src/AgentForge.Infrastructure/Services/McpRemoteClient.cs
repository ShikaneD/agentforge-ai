using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using AgentForge.Application.Contracts;
using Microsoft.Extensions.Configuration;

namespace AgentForge.Infrastructure.Services;

public sealed class McpRemoteClient(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IMcpRemoteClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<McpRemoteTool>> ListToolsAsync(CancellationToken cancellationToken)
    {
        var tools = new List<McpRemoteTool>();
        foreach (var server in GetServers())
        {
            using var response = await SendAsync(server.Name, "tools/list", new { }, cancellationToken);
            using var document = await ParseResponseAsync(response, cancellationToken);
            if (!document.RootElement.TryGetProperty("result", out var result)
                || !result.TryGetProperty("tools", out var remoteTools)
                || remoteTools.ValueKind != JsonValueKind.Array)
            {
                throw new HttpRequestException($"MCP server '{server.Name}' returned an invalid tools/list response.");
            }

            foreach (var tool in remoteTools.EnumerateArray())
            {
                if (!tool.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var description = tool.TryGetProperty("description", out var descriptionValue)
                    ? descriptionValue.GetString() ?? string.Empty
                    : string.Empty;
                var schema = tool.TryGetProperty("inputSchema", out var schemaValue)
                    ? schemaValue.Clone()
                    : JsonSerializer.SerializeToElement(new { });
                tools.Add(new McpRemoteTool(server.Name, name.GetString()!, description, schema));
            }
        }

        return tools;
    }

    public async Task<JsonElement> CallToolAsync(
        string serverName,
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var knownTools = await ListToolsAsync(cancellationToken);
        if (!knownTools.Any(tool =>
                tool.Server.Equals(serverName, StringComparison.Ordinal)
                && tool.Name.Equals(toolName, StringComparison.Ordinal)))
        {
            throw new ArgumentException("The selected remote MCP tool is not configured or available.");
        }

        using var response = await SendAsync(
            serverName,
            "tools/call",
            new { name = toolName, arguments },
            cancellationToken);
        using var document = await ParseResponseAsync(response, cancellationToken);
        if (document.RootElement.TryGetProperty("error", out var error))
        {
            throw new HttpRequestException($"Remote MCP tool '{toolName}' failed: {error.GetRawText()}");
        }

        return document.RootElement.TryGetProperty("result", out var result)
            ? result.Clone()
            : throw new HttpRequestException($"MCP server '{serverName}' returned an invalid tool result.");
    }

    private async Task<HttpResponseMessage> SendAsync(
        string serverName,
        string method,
        object parameters,
        CancellationToken cancellationToken)
    {
        var server = GetServers().FirstOrDefault(server => server.Name.Equals(serverName, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"MCP server '{serverName}' is not configured.");
        var request = new HttpRequestMessage(HttpMethod.Post, server.Url)
        {
            Content = JsonContent.Create(
                new McpRequest("2.0", Guid.NewGuid().ToString("N"), method, parameters),
                options: JsonOptions)
        };
        request.Headers.Accept.ParseAdd("application/json");
        if (!string.IsNullOrWhiteSpace(server.BearerToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", server.BearerToken);
        }

        var response = await httpClientFactory.CreateClient("mcp").SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            response.Dispose();
            throw new HttpRequestException($"MCP server '{serverName}' returned HTTP {statusCode}.");
        }

        return response;
    }

    private IReadOnlyList<ConfiguredServer> GetServers()
    {
        return configuration.GetSection("Mcp:Servers").GetChildren()
            .Select(section =>
            {
                var value = section["Url"];
                if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttps
                        && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
                    || !string.IsNullOrEmpty(uri.UserInfo))
                {
                    throw new InvalidOperationException($"MCP server '{section.Key}' must have an HTTPS URL (HTTP is allowed for localhost).");
                }

                return new ConfiguredServer(section.Key, uri, section["BearerToken"]);
            })
            .ToArray();
    }

    private static async Task<JsonDocument> ParseResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
        }
        finally
        {
            response.Dispose();
        }
    }

    private sealed record ConfiguredServer(string Name, Uri Url, string? BearerToken);
    private sealed record McpRequest(
        string Jsonrpc,
        string Id,
        string Method,
        [property: JsonPropertyName("params")]
        object Parameters);
}
