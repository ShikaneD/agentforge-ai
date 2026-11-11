using AgentForge.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Infrastructure.Data;

public sealed class AgentForgeDbContext(DbContextOptions<AgentForgeDbContext> options) : DbContext(options)
{
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(agent => agent.Id);
            entity.Property(agent => agent.Name).HasMaxLength(100).IsRequired();
            entity.Property(agent => agent.Description).HasMaxLength(500).IsRequired();
            entity.Property(agent => agent.ModelProvider).HasMaxLength(30).IsRequired();
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(conversation => conversation.Id);
            entity.Property(conversation => conversation.Title).HasMaxLength(120).IsRequired();
            entity.HasIndex(conversation => conversation.UpdatedAtUtc);
            entity.HasOne<Agent>()
                .WithMany()
                .HasForeignKey(conversation => conversation.AgentId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(conversation => conversation.Messages)
                .WithOne()
                .HasForeignKey(message => message.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConversationMessage>(entity =>
        {
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Role).HasMaxLength(20).IsRequired();
            entity.Property(message => message.Content).HasMaxLength(24000).IsRequired();
            entity.HasIndex(message => new { message.ConversationId, message.CreatedAtUtc });
        });

        modelBuilder.Entity<KnowledgeDocument>(entity =>
        {
            entity.HasKey(document => document.Id);
            entity.Property(document => document.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(document => document.CreatedAtUtc);
            entity.HasMany(document => document.Chunks)
                .WithOne(chunk => chunk.Document)
                .HasForeignKey(chunk => chunk.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeChunk>(entity =>
        {
            entity.HasKey(chunk => chunk.Id);
            entity.Property(chunk => chunk.Content).HasMaxLength(4000).IsRequired();
            entity.HasIndex(chunk => new { chunk.DocumentId, chunk.Position }).IsUnique();
        });

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Subject).HasMaxLength(200).IsRequired();
            entity.Property(entry => entry.Action).HasMaxLength(100).IsRequired();
            entity.Property(entry => entry.ResourceType).HasMaxLength(100).IsRequired();
            entity.Property(entry => entry.ResourceId).HasMaxLength(100).IsRequired();
            entity.HasIndex(entry => entry.CreatedAtUtc);
        });
    }
}
