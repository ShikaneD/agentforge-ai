namespace AgentForge.Application.Contracts;

public sealed record ModelMessage(string Role, string Content);

public sealed record ModelCompletionRequest(
    string Model,
    string SystemPrompt,
    IReadOnlyList<ModelMessage> Messages,
    bool RequireJson);

public interface IModelProvider
{
    string Name { get; }
    string DefaultModel { get; }
    Task<string> CompleteAsync(ModelCompletionRequest request, CancellationToken cancellationToken);
}

public interface IModelProviderRegistry
{
    IModelProvider GetProvider(string name);
}
