using chatbotchaytau.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Pgvector;

namespace chatbotchaytau.Services.Ingestion;

/// <summary>
/// Chunks and embeds one uploaded document into Postgres. Replaces the whole-folder
/// ingestion pipeline: one file in, one document's chunks out, scoped to its owner.
/// </summary>
public class DocumentIngestor(
    IServiceScopeFactory scopeFactory,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    ILogger<DocumentIngestor> logger)
{
    private const int MaxChunkChars = 1200;
    private const int OverlapChars = 150;

    public async Task<int> IngestAsync(
        FileInfo file, string documentId, string ownerId, CancellationToken ct = default)
    {
        var reader = new DocumentReader(file.Directory!);
        var document = await reader.ReadAsync(file, documentId, cancellationToken: ct);

        var text = string.Join("\n\n", document.EnumerateContent()
            .Select(e => e.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t)));

        var pieces = Chunk(text);
        if (pieces.Count == 0)
        {
            logger.LogWarning("Document '{DocumentId}' produced no extractable text.", documentId);
            return 0;
        }

        var embeddings = await embeddingGenerator.GenerateAsync(pieces, cancellationToken: ct);

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Chunks
            .Where(c => c.OwnerId == ownerId && c.DocumentId == documentId)
            .ExecuteDeleteAsync(ct);

        for (var i = 0; i < pieces.Count; i++)
        {
            db.Chunks.Add(new Chunk
            {
                OwnerId = ownerId,
                DocumentId = documentId,
                Text = pieces[i],
                Embedding = new Vector(embeddings[i].Vector.ToArray()),
            });
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogInformation("Ingested '{DocumentId}' as {Count} chunks for owner {OwnerId}.",
            documentId, pieces.Count, ownerId);
        return pieces.Count;
    }

    internal static List<string> Chunk(string text)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;

        var position = 0;
        while (position < text.Length)
        {
            var length = Math.Min(MaxChunkChars, text.Length - position);

            // Prefer cutting at a paragraph, then sentence boundary.
            if (position + length < text.Length)
            {
                var window = text.AsSpan(position, length);
                var boundary = window.LastIndexOf("\n\n", StringComparison.Ordinal);
                if (boundary < MaxChunkChars / 2)
                    boundary = window.LastIndexOf(". ", StringComparison.Ordinal);

                if (boundary > MaxChunkChars / 4)
                    length = boundary + 1;
            }

            var piece = text.Substring(position, length).Trim();
            if (piece.Length > 0) chunks.Add(piece);

            // The window consumed the end of the text: stop. Without this, the
            // tail shrinks below OverlapChars and Math.Max(1, ...) below would
            // slide one character at a time, emitting a sliver chunk per char.
            if (position + length >= text.Length) break;

            position += Math.Max(1, length - OverlapChars);
        }

        return chunks;
    }
}
