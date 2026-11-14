using aida.Services.Documents;
using Microsoft.Extensions.Logging.Abstractions;

namespace aida.Tests.Services;

public class DocumentServiceTests
{
    [Theory]
    [InlineData("report.pdf", true)]
    [InlineData("REPORT.PDF", true)]   // extension check is case-insensitive
    [InlineData("notes.md", true)]
    [InlineData("sheet.xlsx", true)]
    [InlineData("word.docx", true)]
    [InlineData("script.txt", false)]
    [InlineData("malware.exe", false)]
    [InlineData("noextension", false)]
    [InlineData("archive.pdf.exe", false)] // checks the LAST extension
    [InlineData("../../etc/passwd", false)] // path stripped; "passwd" has no extension
    [InlineData("../../evil.pdf", true)]    // path stripped to "evil.pdf"
    public void IsSupportedFileName(string fileName, bool expected)
    {
        Assert.Equal(expected, DocumentService.IsSupportedFileName(fileName));
    }

    [Fact]
    public async Task SaveAndIngest_rejects_unsupported_files_before_touching_dependencies()
    {
        // Validation runs before the environment/ingestor are used, so nulls here
        // prove the guard fires first — no filesystem or database access happens.
        var service = new DocumentService(
            env: null!,
            ingestor: null!,
            logger: NullLogger<DocumentService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveAndIngestAsync(new MemoryStream(), "malware.exe", "owner-1"));

        Assert.Contains(".exe", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
