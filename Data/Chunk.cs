using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace chatbotchaytau.Data;

public class Chunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = default!;
    public string DocumentId { get; set; } = default!;
    public string Text { get; set; } = default!;
    public string? Context { get; set; }

    [Column(TypeName = "vector(1536)")]
    public Vector? Embedding { get; set; }
}
