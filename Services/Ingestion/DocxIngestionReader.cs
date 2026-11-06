using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.DataIngestion;

namespace chatbotchaytau.Services.Ingestion;

internal sealed class DocxIngestionReader : IngestionDocumentReader
{
    public override Task<IngestionDocument> ReadAsync(Stream source, string identifier, string mediaType, CancellationToken cancellationToken = default)
    {
        using var doc = WordprocessingDocument.Open(source, false);
        var document = new IngestionDocument(identifier);

        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is not null)
        {
            var section = new IngestionDocumentSection();
            foreach (var para in body.Descendants<Paragraph>())
            {
                var text = para.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                    section.Elements.Add(new IngestionDocumentParagraph(text) { Text = text });
            }
            document.Sections.Add(section);
        }

        return Task.FromResult(document);
    }
}
