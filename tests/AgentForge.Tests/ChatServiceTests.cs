using System.Text.Json;
using AgentForge.Application.Contracts;
using AgentForge.Application.Models;
using AgentForge.Application.Services;
using AgentForge.Domain.Models;
using Xunit;

namespace AgentForge.Tests;

public sealed class ChatServiceTests
{
    [Fact]
    public async Task SendAsync_PersistsConversationAndUsesStructuredModelOutput()
    {
        var agent = new Agent
        {
            Name = "Knowledge Agent",
            Description = "Answers questions.",
            Role = AgentRole.Knowledge,
            ModelProvider = "Ollama"
        };
        var catalog = new FakeAgentCatalog(agent);
        var conversations = new FakeConversationStore();
        var provider = new FakeModelProvider("{\"message\":\"The answer is 42.\"}");
        var audit = new FakeAuditLogger();
        var service = new ChatService(
            catalog,
            conversations,
            new FakeKnowledgeStore(),
            new FakeProviderRegistry(provider),
            new AgentToolExecutor(catalog, new FakeKnowledgeStore(), new FakeProviderRegistry(provider), audit),
            audit);

        var response = await service.SendAsync(
            new ChatRequest { Message = "What is the answer?" },
            "test-user",
            CancellationToken.None);

        Assert.Equal(agent.Id, response.AgentId);
        Assert.Equal("The answer is 42.", response.AssistantMessage);
        Assert.Equal(2, conversations.Items.Single().Messages.Count);
        Assert.Equal(["user", "assistant"], conversations.Items.Single().Messages.Select(message => message.Role));
        Assert.Equal("What is the answer?", provider.Requests.Single().Messages.Single().Content);
        Assert.Equal(1, audit.Count);
    }

    [Fact]
    public async Task SendAsync_ExecutesOnlyAllowListedToolsAndValidatesFinalOutput()
    {
        var agent = new Agent
        {
            Name = "Knowledge Agent",
            Description = "Answers questions.",
            Role = AgentRole.Knowledge,
            ModelProvider = "Ollama"
        };
        var catalog = new FakeAgentCatalog(agent);
        var conversations = new FakeConversationStore();
        var provider = new FakeModelProvider(
            "{\"message\":\"\",\"toolCalls\":[{\"name\":\"knowledge.search\",\"arguments\":{\"query\":\"policy\"}}]}",
            "{\"message\":\"Found source guidance.\"}");
        var knowledge = new FakeKnowledgeStore();
        var registry = new FakeProviderRegistry(provider);
        var service = new ChatService(
            catalog,
            conversations,
            knowledge,
            registry,
            new AgentToolExecutor(catalog, knowledge, registry, new FakeAuditLogger()),
            new FakeAuditLogger());

        var response = await service.SendAsync(
            new ChatRequest { Message = "Find the policy." },
            "test-user",
            CancellationToken.None);

        Assert.Equal("Found source guidance.", response.AssistantMessage);
        Assert.Equal(2, provider.Requests.Count);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new AgentToolExecutor(catalog, knowledge, registry, new FakeAuditLogger()).ExecuteAsync(
                "system.exec",
                JsonDocument.Parse("{}").RootElement,
                agent.Id,
                CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_RejectsMalformedStructuredOutput()
    {
        var agent = new Agent { Name = "Agent", Role = AgentRole.Knowledge, ModelProvider = "Ollama" };
        var provider = new FakeModelProvider("not-json");
        var catalog = new FakeAgentCatalog(agent);
        var knowledge = new FakeKnowledgeStore();
        var registry = new FakeProviderRegistry(provider);
        var service = new ChatService(
            catalog,
            new FakeConversationStore(),
            knowledge,
            registry,
            new AgentToolExecutor(catalog, knowledge, registry, new FakeAuditLogger()),
            new FakeAuditLogger());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendAsync(
            new ChatRequest { Message = "Hello" },
            "test-user",
            CancellationToken.None));
    }

    private sealed class FakeAgentCatalog(Agent agent) : IAgentCatalog
    {
        public Task<IReadOnlyList<Agent>> GetAgentsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Agent>>([agent]);
        public Task<Agent?> GetAgentAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Agent?>(id == agent.Id ? agent : null);
        public Task<Agent> CreateAgentAsync(Agent value, CancellationToken cancellationToken) => Task.FromResult(value);
        public IReadOnlyList<ModelProviderInfo> GetSupportedProviders() => [];
    }

    private sealed class FakeConversationStore : IConversationStore
    {
        public List<Conversation> Items { get; } = [];
        public Task<IReadOnlyList<Conversation>> GetConversationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Conversation>>(Items);
        public Task<Conversation?> GetConversationAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
        public Task<Conversation> AddConversationAsync(string title, Guid? agentId, string firstMessage, CancellationToken cancellationToken)
        {
            var conversation = new Conversation
            {
                AgentId = agentId,
                Title = title,
                Messages = [new ConversationMessage { Role = "user", Content = firstMessage }]
            };
            Items.Add(conversation);
            return Task.FromResult(conversation);
        }
        public Task<Conversation> AddMessageAsync(Guid conversationId, string role, string content, CancellationToken cancellationToken)
        {
            var conversation = Items.Single(item => item.Id == conversationId);
            conversation.Messages.Add(new ConversationMessage { Role = role, Content = content });
            return Task.FromResult(conversation);
        }
    }

    private sealed class FakeKnowledgeStore : IKnowledgeStore
    {
        public Task<KnowledgeDocument> AddDocumentAsync(string name, string content, CancellationToken cancellationToken) =>
            Task.FromResult(new KnowledgeDocument { Name = name });
        public Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(string query, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<KnowledgeChunk>>([]);
    }

    private sealed class FakeModelProvider(params string[] responses) : IModelProvider
    {
        private int _responseIndex;
        public string Name => "Ollama";
        public string DefaultModel => "test-model";
        public List<ModelCompletionRequest> Requests { get; } = [];
        public Task<string> CompleteAsync(ModelCompletionRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var index = Math.Min(_responseIndex++, responses.Length - 1);
            return Task.FromResult(responses[index]);
        }
    }

    private sealed class FakeProviderRegistry(IModelProvider provider) : IModelProviderRegistry
    {
        public IModelProvider GetProvider(string name) =>
            name == provider.Name ? provider : throw new InvalidOperationException();
    }

    private sealed class FakeAuditLogger : IAuditLogger
    {
        public int Count { get; private set; }
        public Task WriteAsync(string subject, string action, string resourceType, string resourceId, CancellationToken cancellationToken)
        {
            Count++;
            return Task.CompletedTask;
        }
    }
}
