using System.Linq.Expressions;
using System.Text.RegularExpressions;
using AgentForge.Application.Contracts;
using AgentForge.Domain.Models;
using AgentForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Infrastructure.Services;

public sealed partial class EfKnowledgeStore(AgentForgeDbContext dbContext) : IKnowledgeStore
{
    private const int ChunkSize = 3000;

    public async Task<KnowledgeDocument> AddDocumentAsync(string name, string content, CancellationToken cancellationToken)
    {
        var document = new KnowledgeDocument { Name = name.Trim() };
        var chunks = SplitIntoChunks(content);
        for (var index = 0; index < chunks.Count; index++)
        {
            document.Chunks.Add(new KnowledgeChunk { Position = index, Content = chunks[index] });
        }

        dbContext.KnowledgeDocuments.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        var terms = WordsRegex().Matches(query)
            .Select(match => match.Value.ToLowerInvariant())
            .Where(term => term.Length >= 3)
            .Distinct()
            .Take(8)
            .ToArray();
        if (terms.Length == 0)
        {
            return [];
        }

        var predicate = BuildKeywordPredicate(terms);
        var candidates = await dbContext.KnowledgeChunks.AsNoTracking()
            .Include(chunk => chunk.Document)
            .Where(predicate)
            .Take(100)
            .ToListAsync(cancellationToken);

        return candidates
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = terms.Count(term => chunk.Content.Contains(term, StringComparison.OrdinalIgnoreCase))
            })
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Chunk.Position)
            .Take(Math.Clamp(limit, 1, 10))
            .Select(result => result.Chunk)
            .ToArray();
    }

    private static Expression<Func<KnowledgeChunk, bool>> BuildKeywordPredicate(IReadOnlyList<string> terms)
    {
        var parameter = Expression.Parameter(typeof(KnowledgeChunk), "chunk");
        var content = Expression.Property(parameter, nameof(KnowledgeChunk.Content));
        Expression body = Expression.Constant(false);
        foreach (var term in terms)
        {
            var contains = Expression.Call(
                content,
                nameof(string.Contains),
                Type.EmptyTypes,
                Expression.Constant(term));
            body = Expression.OrElse(body, contains);
        }

        return Expression.Lambda<Func<KnowledgeChunk, bool>>(body, parameter);
    }

    private static List<string> SplitIntoChunks(string content)
    {
        var chunks = new List<string>();
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        for (var start = 0; start < normalized.Length; start += ChunkSize)
        {
            var length = Math.Min(ChunkSize, normalized.Length - start);
            if (start + length < normalized.Length)
            {
                var boundary = normalized.LastIndexOf('\n', start + length - 1, length);
                if (boundary > start + (ChunkSize / 2))
                {
                    length = boundary - start;
                }
            }

            var chunk = normalized.Substring(start, length).Trim();
            if (chunk.Length > 0)
            {
                chunks.Add(chunk);
            }

            start += length - ChunkSize;
        }

        return chunks;
    }

    [GeneratedRegex(@"[\p{L}\p{N}_-]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordsRegex();
}
