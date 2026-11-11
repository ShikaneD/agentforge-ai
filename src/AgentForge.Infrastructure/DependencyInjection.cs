using AgentForge.Application.Contracts;
using AgentForge.Application.Services;
using AgentForge.Infrastructure.Data;
using AgentForge.Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentForge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AgentForge");
        var databaseServer = configuration["Database:Server"];
        var databasePassword = configuration["MSSQL_SA_PASSWORD"];
        if (!string.IsNullOrWhiteSpace(databaseServer) && !string.IsNullOrWhiteSpace(databasePassword))
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                DataSource = databaseServer,
                InitialCatalog = "AgentForge",
                UserID = "sa",
                Password = databasePassword,
                IntegratedSecurity = false,
                Encrypt = true,
                TrustServerCertificate = true
            };
            connectionString = builder.ConnectionString;
        }

        connectionString ??= "Server=localhost;Database=AgentForge;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

        services.AddDbContext<AgentForgeDbContext>(options => options.UseSqlServer(
            connectionString,
            sqlServer => sqlServer.EnableRetryOnFailure(3)));
        services.AddHttpClient("llm", client => client.Timeout = TimeSpan.FromSeconds(90))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(10)
            });
        services.AddHttpClient("mcp", client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddHttpClient("a2a", client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddScoped<IAgentCatalog, EfAgentCatalog>();
        services.AddScoped<IConversationStore, EfConversationStore>();
        services.AddScoped<IKnowledgeStore, EfKnowledgeStore>();
        services.AddScoped<IAuditLogger, EfAuditLogger>();
        services.AddSingleton<IMcpRemoteClient, McpRemoteClient>();
        services.AddSingleton<IA2ARemoteClient, A2ARemoteClient>();
        services.AddSingleton<IModelProviderRegistry, ModelProviderRegistry>();
        services.AddScoped<IAgentToolExecutor, AgentToolExecutor>();
        services.AddScoped<AgentOrchestrator>();
        services.AddScoped<ChatService>();

        return services;
    }
}
