using aida.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Pgvector;

namespace aida.Services;

/// <summary>Hybrid retrieval over a single owner's chunks.</summary>
public class SemanticSearch(
    IServiceScopeFactory scopeFactory,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
{
    private const int CandidateCount = 50;
    private const int RrfK = 60;

    private sealed class Hit
    {
        public Guid Id { get; set; }
        public string DocumentId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string? Context { get; set; }
        public double Score { get; set; }
    }

    public async Task<IReadOnlyList<IngestedChunk>> SearchAsync(
        string text, string ownerId, string? documentIdFilter, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || string.IsNullOrWhiteSpace(text))
            return [];

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var embedded = await embeddingGenerator.GenerateAsync([text]);
        var query = new Vector(embedded[0].Vector.ToArray());

        // Empty string means "no document filter" so the parameter is never NULL
        // and Npgsql can still infer its type.
        var filter = documentIdFilter ?? string.Empty;

        // Hybrid retrieval: vector KNN and full-text ranking are each scored with
        // reciprocal rank fusion, so exact tokens (part numbers, names) survive.
        var hits = await db.Database.SqlQuery<Hit>($"""
            WITH vec AS (
                SELECT c."Id",
                       ROW_NUMBER() OVER (ORDER BY c."Embedding" <=> {query}) AS rank
                FROM "Chunks" c
                WHERE c."OwnerId" = {ownerId}
                  AND ({filter} = '' OR c."DocumentId" = {filter})
                ORDER BY c."Embedding" <=> {query}
                LIMIT {CandidateCount}
            ),
            kw AS (
                SELECT c."Id",
                       ROW_NUMBER() OVER (
                           ORDER BY ts_rank_cd(to_tsvector('simple', c."Text"),
                                               websearch_to_tsquery('simple', {text})) DESC
                       ) AS rank
                FROM "Chunks" c
                WHERE c."OwnerId" = {ownerId}
                  AND ({filter} = '' OR c."DocumentId" = {filter})
                  AND to_tsvector('simple', c."Text") @@ websearch_to_tsquery('simple', {text})
                LIMIT {CandidateCount}
            ),
            fused AS (
                SELECT COALESCE(vec."Id", kw."Id") AS id,
                       COALESCE(1.0 / ({RrfK} + vec.rank), 0)
                     + COALESCE(1.0 / ({RrfK} + kw.rank), 0) AS score
                FROM vec
                FULL OUTER JOIN kw ON kw."Id" = vec."Id"
            )
            SELECT c."Id"         AS "Id",
                   c."DocumentId" AS "DocumentId",
                   c."Text"       AS "Text",
                   c."Context"    AS "Context",
                   f.score        AS "Score"
            FROM fused f
            JOIN "Chunks" c ON c."Id" = f.id
            ORDER BY f.score DESC
            LIMIT {maxResults}
            """).ToListAsync();

        return hits
            .Select(h => new IngestedChunk
            {
                DocumentId = h.DocumentId,
                Text = h.Text,
                Context = h.Context,
            })
            .ToList();
    }
}
