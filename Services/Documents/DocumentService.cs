using aida.Services.Ingestion;

namespace aida.Services.Documents;

/// <summary>Saves an uploaded file under its owner and ingests it.</summary>
public class DocumentService(
    IWebHostEnvironment env,
    DocumentIngestor ingestor,
    ILogger<DocumentService> logger)
{
    private static readonly HashSet<string> AllowedExtensions = [".pdf", ".md", ".docx", ".xlsx"];

    /// <summary>True if the file name (after stripping any path) has an allowed extension.</summary>
    internal static bool IsSupportedFileName(string fileName)
    {
        var extension = Path.GetExtension(Path.GetFileName(fileName)).ToLowerInvariant();
        return AllowedExtensions.Contains(extension);
    }

    public async Task<string> SaveAndIngestAsync(
        Stream content, string fileName, string ownerId, CancellationToken ct = default)
    {
        var safeName = Path.GetFileName(fileName);

        if (!IsSupportedFileName(safeName))
        {
            var extension = Path.GetExtension(safeName).ToLowerInvariant();
            throw new InvalidOperationException($"Unsupported file type '{extension}'.");
        }

        var directory = Path.Combine(env.ContentRootPath, "App_Data", "documents", ownerId);
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, safeName);
        await using (var target = File.Create(path))
        {
            await content.CopyToAsync(target, ct);
        }

        await ingestor.IngestAsync(new FileInfo(path), safeName, ownerId, ct);
        logger.LogInformation("Stored and ingested '{FileName}' for owner {OwnerId}.", safeName, ownerId);
        return safeName;
    }
}
