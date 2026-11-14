namespace aida.Services;

public class IngestedChunk
{
    public required string DocumentId { get; init; }
    public required string Text { get; init; }
    public string? Context { get; init; }
}
