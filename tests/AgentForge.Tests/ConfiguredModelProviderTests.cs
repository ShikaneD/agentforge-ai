using AgentForge.Application.Contracts;
using AgentForge.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace AgentForge.Tests;

public sealed class ConfiguredModelProviderTests
{
    [Fact]
    public async Task CompleteAsync_ReportsMissingGeminiKeyAsConfigurationError()
    {
        var configuration = new ConfigurationBuilder().Build();
        using var httpClient = new HttpClient(new UnusedHttpMessageHandler());
        var provider = new ConfiguredModelProvider("Gemini", httpClient, configuration);
        var request = new ModelCompletionRequest(
            "gemini-2.5-flash",
            "system prompt",
            [new ModelMessage("user", "Hello")],
            true);

        var exception = await Assert.ThrowsAsync<ModelProviderConfigurationException>(() =>
            provider.CompleteAsync(request, CancellationToken.None));

        Assert.Contains("Models:Gemini:ApiKey", exception.Message);
    }

    private sealed class UnusedHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The HTTP handler should not be called.");
    }
}
