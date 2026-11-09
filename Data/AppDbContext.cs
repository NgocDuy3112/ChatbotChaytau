using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace chatbotchaytau.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser>(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessageRow> ChatMessages => Set<ChatMessageRow>();
    public DbSet<Chunk> Chunks => Set<Chunk>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("vector");

        builder.Entity<Conversation>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Title).HasMaxLength(200).IsRequired();
            e.HasOne(c => c.User).WithMany()
             .HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => new { c.UserId, c.UpdatedAt });
        });

        builder.Entity<ChatMessageRow>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Role).HasMaxLength(32).IsRequired();
            e.Property(m => m.Text).IsRequired();
            e.HasOne(m => m.Conversation).WithMany(c => c.Messages)
             .HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => new { m.ConversationId, m.Ordinal });
        });

        builder.Entity<Chunk>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.OwnerId).HasMaxLength(64).IsRequired();
            e.Property(c => c.DocumentId).HasMaxLength(512).IsRequired();
            e.HasIndex(c => new { c.OwnerId, c.DocumentId });
            e.Property(c => c.Text).IsRequired();

            // Approximate nearest-neighbour index for cosine distance (<=>).
            e.HasIndex(c => c.Embedding)
             .HasMethod("hnsw")
             .HasOperators("vector_cosine_ops");
        });
    }
}
