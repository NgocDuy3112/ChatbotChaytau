using aida.Services.Ingestion;

namespace aida.Tests.Services;

public class ChunkingTests
{
    private const int MaxChunkChars = 1200;
    private const int OverlapChars = 150;

    /// <summary>Unique, whitespace-free text so every chunk is a distinct
    /// substring and Trim() is a no-op — offsets stay exact.</summary>
    private static string UniqueText(int items) =>
        string.Concat(Enumerable.Range(0, items).Select(i => $"w{i:D6}"));

    private static int StartOf(string text, string chunk, int searchFrom) =>
        text.IndexOf(chunk, searchFrom, StringComparison.Ordinal);

    [Fact]
    public void Empty_and_whitespace_produce_no_chunks()
    {
        Assert.Empty(DocumentIngestor.Chunk(""));
        Assert.Empty(DocumentIngestor.Chunk("   \n\n  "));
    }

    [Fact]
    public void Short_text_is_a_single_chunk()
    {
        var text = "Hello world, this is a short document.";

        var chunks = DocumentIngestor.Chunk(text);

        var chunk = Assert.Single(chunks);
        Assert.Equal(text, chunk);
    }

    [Fact]
    public void Long_text_respects_max_chunk_size()
    {
        var chunks = DocumentIngestor.Chunk(UniqueText(1700)); // ~10 200 chars

        Assert.All(chunks, c => Assert.InRange(c.Length, 1, MaxChunkChars));
        Assert.True(chunks.Count >= 5, $"Expected several chunks, got {chunks.Count}");
        // Regression: the tail must not degenerate into ~150 one-char-step
        // slivers (the old loop advanced by max(1, length-150) on the last
        // window, re-chunking the final 150 chars over and over).
        Assert.True(chunks.Count <= 15, $"Expected ≤15 chunks, got {chunks.Count}");
    }

    [Fact]
    public void Consecutive_chunks_overlap_by_exactly_the_overlap_size()
    {
        var text = UniqueText(1700);
        var chunks = DocumentIngestor.Chunk(text);

        var previousStart = -1;
        var previousLength = -1;
        foreach (var chunk in chunks)
        {
            var start = StartOf(text, chunk, previousStart + 1);
            Assert.True(start >= 0, "Chunk not found in source text in order.");

            if (previousStart >= 0)
            {
                var overlap = previousStart + previousLength - start;
                Assert.Equal(OverlapChars, overlap);
            }

            previousStart = start;
            previousLength = chunk.Length;
        }
    }

    [Fact]
    public void Chunks_cover_the_entire_text()
    {
        var text = UniqueText(1700);
        var chunks = DocumentIngestor.Chunk(text);

        Assert.Equal(0, StartOf(text, chunks[0], 0));
        var last = chunks[^1];
        Assert.EndsWith(text[^40..], last, StringComparison.Ordinal);
    }

    [Fact]
    public void Prefers_paragraph_boundary_over_window_edge()
    {
        var text = new string('A', 700) + "\n\n" + new string('B', 700);

        var chunks = DocumentIngestor.Chunk(text);

        // First cut happens at the paragraph break (700), not the 1200 window.
        Assert.Equal(new string('A', 700), chunks[0]);
        Assert.Equal(2, chunks.Count);
        Assert.Contains("\n\n", chunks[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Prefers_sentence_boundary_when_no_paragraph_break()
    {
        var text = string.Join(" ",
            Enumerable.Range(0, 60).Select(i => $"Sentence number {i} is here."));

        var chunks = DocumentIngestor.Chunk(text);

        // The window edge would be exactly 1200; a sentence cut lands earlier.
        Assert.True(chunks[0].Length < MaxChunkChars,
            $"Expected a sentence cut, got the full window ({chunks[0].Length}).");
        Assert.EndsWith(".", chunks[0], StringComparison.Ordinal);
        Assert.True(chunks.Count <= 4, $"Expected ≤4 chunks, got {chunks.Count}");
    }

    [Fact]
    public void Large_plain_text_does_not_degenerate_into_slivers()
    {
        var chunks = DocumentIngestor.Chunk(new string('x', 5000));

        Assert.True(chunks.Count <= 10, $"Expected ≤10 chunks, got {chunks.Count}");
        Assert.All(chunks, c => Assert.True(c.Length >= 151,
            $"Degenerate sliver chunk of length {c.Length}."));
    }
}
