using System.Text.Json;
using System.Net.Http.Json;
using AgentForge.Application.Contracts;
using AgentForge.Application.Models;
using Microsoft.Extensions.Configuration;

namespace AgentForge.Infrastructure.Services;

public sealed class A2ARemoteClient(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IA2ARemoteClient
{
    public async Task<IReadOnlyList<A2ARemoteAgent>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var discovered = new List<A2ARemoteAgent>();
        foreach (var agent in GetConfiguredAgents())
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(agent.Url, "/.well-known/agent-card.json"));
            AddBearerToken(request, agent.BearerToken);
            using var response = await httpClientFactory.CreateClient("a2a").SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var card = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            var name = card.RootElement.TryGetProperty("name", out var value)
                ? value.GetString() ?? agent.Name
                : agent.Name;
            discovered.Add(new A2ARemoteAgent(name, agent.Url.ToString(), card.RootElement.Clone()));
        }

        return discovered;
    }

    public async Task<JsonElement> SendTaskAsync(
        string agentName,
        AgentTaskRequest request,
        CancellationToken cancellationToken)
    {
        var agent = GetConfiguredAgents().FirstOrDefault(candidate =>
            candidate.Name.Equals(agentName, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"A2A agent '{agentName}' is not configured.");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(agent.Url, "/a2a/tasks"))
        {
            Content = JsonContent.Create(request)
        };
        AddBearerToken(httpRequest, agent.BearerToken);
        using var response = await httpClientFactory.CreateClient("a2a").SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"A2A agent '{agentName}' returned HTTP {(int)response.StatusCode}.");
        }

        using var result = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        return result.RootElement.Clone();
    }

    private IReadOnlyList<ConfiguredAgent> GetConfiguredAgents()
    {
        return configuration.GetSection("A2A:Agents").GetChildren()
            .Select(section =>
            {
                var value = section["Url"];
                if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttps
                        && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
                    || !string.IsNullOrEmpty(uri.UserInfo))
                {
                    throw new InvalidOperationException($"A2A agent '{section.Key}' must have an HTTPS URL (HTTP is allowed for localhost).");
                }

                return new ConfiguredAgent(section.Key, uri, section["BearerToken"]);
            })
            .ToArray();
    }

    private static void AddBearerToken(HttpRequestMessage request, string? bearerToken)
    {
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
        }
    }

    private sealed record ConfiguredAgent(string Name, Uri Url, string? BearerToken);
}
