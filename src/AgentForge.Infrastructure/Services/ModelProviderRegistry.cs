using AgentForge.Application.Contracts;
using Microsoft.Extensions.Configuration;

namespace AgentForge.Infrastructure.Services;

public sealed class ModelProviderRegistry(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    : IModelProviderRegistry
{
    public IModelProvider GetProvider(string name)
    {
        if (name is not ("Gemini" or "OpenAI" or "Ollama"))
        {
            throw new InvalidOperationException($"Model provider '{name}' is not supported.");
        }

        var client = httpClientFactory.CreateClient("llm");
        return new ConfiguredModelProvider(name, client, configuration);
    }
}
