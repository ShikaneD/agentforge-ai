using System.Net;
using System.Text;
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

    [Fact]
    public async Task CompleteAsync_UsesGeminiApiKeyEnvironmentVariable()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GEMINI_API_KEY"] = "test-api-key"
            })
            .Build();
        var handler = new CapturingHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var provider = new ConfiguredModelProvider("Gemini", httpClient, configuration);
        var request = new ModelCompletionRequest(
            "gemini-2.5-flash",
            "system prompt",
            [new ModelMessage("user", "Hello")],
            true);

        var response = await provider.CompleteAsync(request, CancellationToken.None);

        Assert.Equal("{\"message\":\"Hello\"}", response);
        Assert.Equal("test-api-key", handler.ApiKey);
    }

    private sealed class UnusedHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The HTTP handler should not be called.");
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public string? ApiKey { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ApiKey = request.Headers.GetValues("x-goog-api-key").Single();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"candidates":[{"content":{"parts":[{"text":"{\"message\":\"Hello\"}"}]}}]}""",
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }
}
