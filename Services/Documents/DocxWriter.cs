using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace aida.Services.Documents;

/// <summary>Markdown-lite → .docx via the OpenXml SDK (already a project
/// dependency). Bullets render as "• " paragraphs — no numbering.xml part in v1.</summary>
internal static class DocxWriter
{
    internal static byte[] Write(string title, string markdown)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document();
            var body = new Body();
            main.Document.Append(body);

            if (!string.IsNullOrWhiteSpace(title))
            {
                body.AppendChild(Heading(title, level: 1));
            }

            foreach (var block in MarkdownLite.Parse(markdown))
            {
                body.AppendChild(block switch
                {
                    MdBlock.Heading h => Heading(h.Text, h.Level),
                    MdBlock.Bullet b => Bullet(b.Text),
                    MdBlock.Code c => Code(c.Text),
                    MdBlock.Paragraph p => Paragraph(p.Text),
                    _ => Paragraph(string.Empty),
                });
            }

            main.Document.Save();
        }

        return stream.ToArray();
    }

    private static Paragraph Heading(string text, int level) =>
        new(new Run(
            new RunProperties(
                new Bold(),
                new FontSize { Val = level switch { 1 => "36", 2 => "30", 3 => "26", _ => "24" } }),
            CreateText(text)));

    private static Paragraph Bullet(string text)
    {
        var paragraph = new Paragraph(
            new ParagraphProperties(new Indentation { Left = "360", Hanging = "360" }));

        paragraph.AppendChild(new Run(CreateText("•  ")));
        foreach (var (part, bold, italic, code) in MarkdownLite.Inline(text))
            paragraph.AppendChild(StyledRun(part, bold, italic, code));

        return paragraph;
    }

    private static Paragraph Code(string text)
    {
        var paragraph = new Paragraph();
        foreach (var (line, index) in text.Split('\n').Select((l, i) => (l, i)))
        {
            if (index > 0) paragraph.AppendChild(new Run(new Break()));
            paragraph.AppendChild(new Run(
                new RunProperties(
                    new RunFonts { Ascii = "Courier New", HighAnsi = "Courier New" },
                    new Shading { Val = ShadingPatternValues.Clear, Fill = "F0F0F0" }),
                CreateText(line)));
        }
        return paragraph;
    }

    private static Paragraph Paragraph(string text)
    {
        var paragraph = new Paragraph();
        foreach (var (part, bold, italic, code) in MarkdownLite.Inline(text))
            paragraph.AppendChild(StyledRun(part, bold, italic, code));
        return paragraph;
    }

    private static Run StyledRun(string text, bool bold, bool italic, bool code)
    {
        var run = new Run();
        var properties = new RunProperties();

        if (bold) properties.AppendChild(new Bold());
        if (italic) properties.AppendChild(new Italic());
        if (code)
        {
            properties.AppendChild(new RunFonts { Ascii = "Courier New", HighAnsi = "Courier New" });
            properties.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = "F0F0F0" });
        }

        if (properties.ChildElements.Count > 0) run.AppendChild(properties);
        run.AppendChild(CreateText(text));
        return run;
    }

    private static Text CreateText(string text) =>
        new(text) { Space = SpaceProcessingModeValues.Preserve };
}
