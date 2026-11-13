using chatbotchaytau.Services.Ingestion;
using Microsoft.Extensions.AI;

namespace chatbotchaytau.Services.Documents;

/// <summary>
/// The assistant's document tools. Functions are created per request with the
/// caller's ownerId captured in the closure — the model never supplies (or can
/// spoof) the owner, so tools can only touch the current user's files.
/// </summary>
internal static class ChatDocumentTools
{
    internal const int MaxReadChars = 60_000;

    internal static AIFunction CreateReadDocument(string ownerId, IWebHostEnvironment env)
    {
        Func<string, Task<string>> read = filename => ReadAsync(ownerId, env, filename);
        return AIFunctionFactory.Create(
            read,
            "read_document",
            "Read the full text of one of the user's stored documents (uploaded earlier or saved by you). " +
            "Call this before editing, extending or rewriting an existing document so no original content is lost. " +
            "The filename is the bare file name, e.g. \"report.docx\".",
            null);
    }

    internal static AIFunction CreateSaveDocument(string ownerId, DocumentService documents)
    {
        Func<string, string, string, Task<string>> save =
            (filename, format, content) => SaveAsync(ownerId, documents, filename, format, content);
        return AIFunctionFactory.Create(
            save,
            "save_document",
            "Create or replace a document file the user can download. 'format' must be exactly \"docx\" (Word) " +
            "or \"pdf\". 'content' is the COMPLETE document in Markdown (headings, bullets, **bold**, `code` " +
            "and ```fences are supported). 'filename' is the bare file name; its extension is corrected to match " +
            "the format. To update an existing document, pass its exact file name to replace it. Afterwards, " +
            "mention the file in your reply with a Markdown download link: [filename](/documents/filename).",
            null);
    }

    /// <summary>Validates tool arguments: safe base name + format → correct
    /// extension. Null when the input cannot be made safe.</summary>
    internal static string? NormalizeFileName(string? filename, string? format)
    {
        var extension = format?.Trim().ToLowerInvariant();
        if (extension is not ("docx" or "pdf")) return null;

        var safe = Path.GetFileName(filename ?? string.Empty);
        if (string.IsNullOrWhiteSpace(safe)) return null;

        return Path.ChangeExtension(safe, extension);
    }

    private static async Task<string> ReadAsync(
        string ownerId, IWebHostEnvironment env, string filename)
    {
        var path = DocumentPaths.Resolve(env.ContentRootPath, ownerId, filename);
        if (path is null || !File.Exists(path))
            return $"Error: no document named '{filename}' belongs to this user.";

        var file = new FileInfo(path);
        try
        {
            var reader = new DocumentReader(file.Directory!);
            var document = await reader.ReadAsync(file, file.Name, cancellationToken: CancellationToken.None);
            var text = string.Join("\n\n",
                document.EnumerateContent()
                    .Select(c => c.Text)
                    .Where(t => !string.IsNullOrWhiteSpace(t)));

            if (text.Length > MaxReadChars)
                text = text[..MaxReadChars]
                    + "\n\n[truncated — the document continues beyond this point]";

            return string.IsNullOrWhiteSpace(text)
                ? "Error: the document has no extractable text (it may be scanned images)."
                : text;
        }
        catch (Exception ex)
        {
            return $"Error: could not read '{filename}': {ex.Message}";
        }
    }

    private static async Task<string> SaveAsync(
        string ownerId, DocumentService documents,
        string? filename, string? format, string? content)
    {
        var safe = NormalizeFileName(filename, format);
        if (safe is null)
            return "Error: filename is required and format must be exactly \"docx\" or \"pdf\".";
        if (string.IsNullOrWhiteSpace(content))
            return "Error: content is empty — provide the full document in Markdown.";

        try
        {
            var title = Path.GetFileNameWithoutExtension(safe);
            var bytes = safe.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                ? PdfWriter.Write(title, content)
                : DocxWriter.Write(title, content);

            await using var stream = new MemoryStream(bytes);
            var saved = await documents.SaveAndIngestAsync(stream, safe, ownerId, CancellationToken.None);

            return $"Saved '{saved}' ({bytes.Length:N0} bytes) and made it searchable. " +
                   $"In your reply, include the download link exactly as: [{saved}](/documents/{saved})";
        }
        catch (Exception ex)
        {
            return $"Error: could not save '{safe}': {ex.Message}";
        }
    }
}
