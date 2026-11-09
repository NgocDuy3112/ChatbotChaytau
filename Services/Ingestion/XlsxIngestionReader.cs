using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.DataIngestion;

namespace chatbotchaytau.Services.Ingestion;

internal sealed class XlsxIngestionReader : IngestionDocumentReader
{
    public override Task<IngestionDocument> ReadAsync(Stream source, string identifier, string mediaType, CancellationToken cancellationToken = default)
    {
        using var doc = SpreadsheetDocument.Open(source, false);
        var document = new IngestionDocument(identifier);

        var workbookPart = doc.WorkbookPart;
        if (workbookPart is null) return Task.FromResult(document);

        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

        var workbook = workbookPart.Workbook;
        if (workbook is null) return Task.FromResult(document);

        foreach (var sheet in workbook.Descendants<Sheet>())
        {
            if (sheet.Id?.Value is not { Length: > 0 } relationshipId) continue;

            var worksheetPart = workbookPart.GetPartById(relationshipId) as WorksheetPart;
            var sheetData = worksheetPart?.Worksheet?.GetFirstChild<SheetData>();
            if (sheetData is null) continue;

            var section = new IngestionDocumentSection();
            foreach (var row in sheetData.Elements<Row>())
            {
                var cells = row.Elements<Cell>().Select(c => GetCellValue(c, sharedStrings)).ToArray();
                var line = string.Join("\t", cells);
                if (!string.IsNullOrWhiteSpace(line))
                    section.Elements.Add(new IngestionDocumentParagraph(line) { Text = line });
            }
            document.Sections.Add(section);
        }

        return Task.FromResult(document);
    }

    private static string GetCellValue(Cell cell, SharedStringTable? sharedStrings)
    {
        if (cell.DataType?.Value == CellValues.SharedString)
        {
            if (int.TryParse(cell.CellValue?.Text, out var idx) && sharedStrings is not null)
                return sharedStrings.ElementAt(idx).InnerText;
            return cell.CellValue?.Text ?? string.Empty;
        }
        return cell.CellValue?.Text ?? string.Empty;
    }
}
