using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace chatbotchaytau.Services.Documents;

/// <summary>Markdown-lite → PDF via QuestPDF. Requires
/// QuestPDF.Settings.License to be set once at startup (Program.cs).</summary>
internal static class PdfWriter
{
    internal static byte[] Write(string title, string markdown)
    {
        var blocks = MarkdownLite.Parse(markdown);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginVertical(2, Unit.Centimetre);
                page.MarginHorizontal(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10.5f));

                page.Header().PaddingBottom(8).Column(col =>
                {
                    col.Item().Text(string.IsNullOrWhiteSpace(title) ? "Document" : title)
                        .FontSize(18).Bold();
                    col.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                });

                page.Content().Column(col =>
                {
                    foreach (var block in blocks)
                    {
                        switch (block)
                        {
                            case MdBlock.Heading h:
                                col.Item()
                                   .PaddingTop(h.Level == 1 ? 10 : 8).PaddingBottom(2)
                                   .Text(MarkdownLite.Plain(h.Text))
                                   .FontSize(h.Level switch { 1 => 16, 2 => 14, 3 => 12.5f, _ => 11.5f })
                                   .Bold();
                                break;

                            case MdBlock.Bullet b:
                                col.Item().PaddingVertical(1).Row(row =>
                                {
                                    row.ConstantItem(14).AlignLeft().Text("•");
                                    row.RelativeItem().Text(MarkdownLite.Plain(b.Text));
                                });
                                break;

                            case MdBlock.Code c:
                                col.Item().PaddingVertical(4).PaddingHorizontal(6)
                                   .Background(Colors.Grey.Lighten4)
                                   .Text(c.Text).FontFamily("Courier New").FontSize(9);
                                break;

                            case MdBlock.Paragraph p:
                                col.Item().PaddingVertical(3).Text(text =>
                                {
                                    foreach (var (part, bold, italic, code) in MarkdownLite.Inline(p.Text))
                                    {
                                        var span = text.Span(part);
                                        if (bold) span = span.Bold();
                                        if (italic) span = span.Italic();
                                        if (code) span = span.FontFamily("Courier New").FontSize(9);
                                    }
                                });
                                break;
                        }
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span($"{title} — ").FontSize(8);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span(" / ").FontSize(8);
                    text.TotalPages().FontSize(8);
                });
            });
        }).GeneratePdf();
    }
}
