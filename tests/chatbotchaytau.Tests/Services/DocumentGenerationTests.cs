using System.Text;
using chatbotchaytau.Services.Documents;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using QuestPDF.Infrastructure;

namespace chatbotchaytau.Tests.Services;

public class DocumentGenerationTests
{
    static DocumentGenerationTests()
    {
        // QuestPDF refuses to render without a license; production sets this
        // in Program.cs, tests need their own.
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
    }

    // --- MarkdownLite ------------------------------------------------------

    [Fact]
    public void Parse_splits_headings_bullets_fences_and_paragraphs()
    {
        var blocks = MarkdownLite.Parse("""
            # Title

            First paragraph
            continues here.

            - one
            - two

            ```csharp
            var x = 1;
            ```
            """);

        Assert.Equal(
        [
            new MdBlock.Heading(1, "Title"),
            new MdBlock.Paragraph("First paragraph continues here."),
            new MdBlock.Bullet("one"),
            new MdBlock.Bullet("two"),
            new MdBlock.Code("var x = 1;"),
        ], blocks);
    }

    [Fact]
    public void Inline_tokenizes_bold_italic_code()
    {
        var runs = MarkdownLite.Inline("say **loud** now").ToList();

        Assert.Equal(("say ", false, false, false), runs[0]);
        Assert.Equal(("loud", true, false, false), runs[1]);
        Assert.Equal((" now", false, false, false), runs[2]);
    }

    [Fact]
    public void Plain_strips_inline_markers()
    {
        Assert.Equal("a b c", MarkdownLite.Plain("a **b** c"));
        Assert.Equal("x y z", MarkdownLite.Plain("x `y` z"));
        Assert.Equal("Tiếng Việt", MarkdownLite.Plain("Tiếng Việt"));
    }

    [Fact]
    public void Parse_on_empty_input_returns_no_blocks()
    {
        Assert.Empty(MarkdownLite.Parse(""));
        Assert.Empty(MarkdownLite.Parse("   \n\n  "));
    }

    // --- .docx -------------------------------------------------------------

    [Fact]
    public void Docx_contains_title_headings_bullets_code_and_vietnamese_text()
    {
        var bytes = DocxWriter.Write("Báo cáo", """
            ## Giới thiệu

            Nội dung **quan trọng** ở đây.

            - mục một
            - mục hai

            ```sql
            SELECT 1;
            ```
            """);

        using var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var body = doc.MainDocumentPart!.Document!;
        var text = string.Join("|", body.Descendants<Text>().Select(t => t.Text));

        Assert.Contains("Báo cáo", text);          // title heading (unicode-safe)
        Assert.Contains("Giới thiệu", text);
        Assert.Contains("mục một", text);
        Assert.Contains("•", text);                 // bullet glyph
        Assert.Contains("SELECT 1;", text);
        Assert.Contains("quan trọng", text);
        Assert.NotEmpty(body.Descendants<Bold>());   // inline bold survives
    }

    [Fact]
    public void Docx_produces_non_trivial_bytes()
    {
        var bytes = DocxWriter.Write("T", "Hello");
        Assert.True(bytes.Length > 1_000, $"docx only {bytes.Length} bytes");
    }

    // --- .pdf --------------------------------------------------------------

    [Fact]
    public void Pdf_is_valid_and_text_is_extractable()
    {
        var bytes = PdfWriter.Write("Quarterly Report", """
            ## Summary

            Hello document readers.

            - bullet point

            ```
            code block
            ```
            """);

        Assert.True(bytes.Length > 1_000, $"pdf only {bytes.Length} bytes");
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);

        using var pdf = PdfDocument.Open(new MemoryStream(bytes));
        var extracted = string.Concat(pdf.GetPages().SelectMany(p => p.Letters).Select(l => l.Value));
        Assert.Contains("Hello document readers", extracted);
    }

    // --- path safety (download endpoint + tools) ---------------------------

    [Theory]
    [InlineData("user-1", "a.pdf", "a.pdf")]           // plain name
    [InlineData("user-1", "../../etc/passwd", "passwd")] // traversal stripped
    [InlineData("user-1", "sub/report.docx", "report.docx")]
    public void Resolve_stays_inside_owner_directory(string owner, string input, string expectedFile)
    {
        var path = DocumentPaths.Resolve("/root", owner, input);

        Assert.NotNull(path);
        Assert.Equal(Path.Combine("/root", "App_Data", "documents", owner, expectedFile), path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("../evil")]   // owner itself must be a plain segment
    public void Resolve_rejects_unsafe_owner(string? owner)
    {
        Assert.Null(DocumentPaths.Resolve("/root", owner, "a.pdf"));
    }

    [Fact]
    public void Resolve_rejects_empty_file_name()
    {
        Assert.Null(DocumentPaths.Resolve("/root", "user-1", ""));
        Assert.Null(DocumentPaths.Resolve("/root", "user-1", "../"));
    }

    // --- tool argument normalization ---------------------------------------

    [Theory]
    [InlineData("report.txt", "pdf", "report.pdf")]     // extension corrected
    [InlineData("report", "docx", "report.docx")]        // extension appended
    [InlineData("../../evil.pdf", "pdf", "evil.pdf")]    // path stripped
    [InlineData("REPORT.PDF", "docx", "REPORT.docx")]    // format wins over stale ext
    public void NormalizeFileName_produces_safe_names(string filename, string format, string expected)
    {
        Assert.Equal(expected, ChatDocumentTools.NormalizeFileName(filename, format));
    }

    [Theory]
    [InlineData("report.pdf", "md")]   // only docx/pdf are writable
    [InlineData("report.pdf", "exe")]
    [InlineData(null, "pdf")]
    [InlineData("   ", "pdf")]
    [InlineData("", "docx")]
    public void NormalizeFileName_rejects_bad_input(string? filename, string? format)
    {
        Assert.Null(ChatDocumentTools.NormalizeFileName(filename, format));
    }
}
