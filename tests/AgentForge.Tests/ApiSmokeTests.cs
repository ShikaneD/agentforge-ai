using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AgentForge.Tests;

public sealed class ApiSmokeTests
{
    [Fact]
    public async Task HealthAndMcpDiscoveryEndpoints_AreAvailable()
    {
        await using var application = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Database:ApplyMigrations", "false"));
        using var client = application.CreateClient();

        var root = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        var rootBody = await root.Content.ReadAsStringAsync();
        Assert.Contains("\"service\":\"AgentForge API\"", rootBody, StringComparison.Ordinal);
        Assert.Contains("\"health\":\"/health\"", rootBody, StringComparison.Ordinal);

        var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        var tools = await client.PostAsJsonAsync("/mcp", new
        {
            jsonrpc = "2.0",
            id = "smoke-test",
            method = "tools/list",
            @params = new { }
        });
        Assert.Equal(HttpStatusCode.OK, tools.StatusCode);
        var body = await tools.Content.ReadAsStringAsync();
        Assert.Contains("knowledge.search", body, StringComparison.Ordinal);
    }
}
