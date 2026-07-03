using System.IO.Compression;
using System.Xml.Linq;
using EmailComposer.Backend.Models;

namespace EmailComposer.Backend.Services;

public sealed class ExcelSourceMailService
{
    private const string RecipientsWorkbookPath = @"c:\icc-mailer\recipients.xlsx";
    private const string SheetName = "Recipients";
    private const string EmailColumnName = "Email";

    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipsNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipsNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    private readonly GraphMailService _graphMailService;

    public ExcelSourceMailService(GraphMailService graphMailService)
    {
        _graphMailService = graphMailService;
    }

    public async Task<ExcelSourceSendResult> SendAsync(
        ExcelSourceSendRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.BodyHtml))
        {
            throw new InvalidOperationException("Subject and email body are required.");
        }

        var recipients = ReadEmailRecipients();
        if (recipients.Count == 0)
        {
            throw new InvalidOperationException(
                $"No email recipients were found in sheet '{SheetName}', column '{EmailColumnName}'.");
        }

        var result = new ExcelSourceSendResult
        {
            RecipientCount = recipients.Count
        };

        foreach (var recipient in recipients)
        {
            await _graphMailService.SendMailAsync(
                new SendMailRequest
                {
                    Subject = request.Subject,
                    BodyHtml = request.BodyHtml,
                    ToRecipients = [recipient]
                },
                cancellationToken);

            result.SentCount++;
        }

        return result;
    }

    private static IReadOnlyList<string> ReadEmailRecipients()
    {
        if (!File.Exists(RecipientsWorkbookPath))
        {
            throw new InvalidOperationException(
                $"Excel recipient workbook was not found: {RecipientsWorkbookPath}");
        }

        using var archive = ZipFile.OpenRead(RecipientsWorkbookPath);
        var sharedStrings = ReadSharedStrings(archive);
        var worksheet = ReadWorksheet(archive);
        var rows = worksheet.Root?
            .Element(SpreadsheetNamespace + "sheetData")?
            .Elements(SpreadsheetNamespace + "row")
            .ToList() ?? [];

        var header = FindEmailHeader(rows, sharedStrings);
        if (header is null)
        {
            throw new InvalidOperationException(
                $"Column '{EmailColumnName}' was not found in sheet '{SheetName}'.");
        }

        return rows
            .Skip(header.RowIndex + 1)
            .Select(row => row
                .Elements(SpreadsheetNamespace + "c")
                .FirstOrDefault(cell => GetCellColumnReference(cell) == header.ColumnReference))
            .Where(cell => cell is not null)
            .Select(cell => GetCellValue(cell!, sharedStrings).Trim())
            .Where(email => email.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static XDocument ReadWorksheet(ZipArchive archive)
    {
        var workbookEntry = archive.GetEntry("xl/workbook.xml")
            ?? throw new InvalidOperationException("Workbook metadata was not found in the Excel file.");
        var workbook = XDocument.Load(workbookEntry.Open());
        var sheet = workbook.Root?
            .Element(SpreadsheetNamespace + "sheets")?
            .Elements(SpreadsheetNamespace + "sheet")
            .FirstOrDefault(element => string.Equals(
                (string?)element.Attribute("name"),
                SheetName,
                StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Sheet '{SheetName}' was not found in the Excel file.");

        var relationshipId = (string?)sheet.Attribute(RelationshipsNamespace + "id")
            ?? throw new InvalidOperationException($"Sheet '{SheetName}' did not include a workbook relationship.");

        var relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels")
            ?? throw new InvalidOperationException("Workbook relationships were not found in the Excel file.");
        var relationships = XDocument.Load(relationshipsEntry.Open());
        var target = relationships.Root?
            .Elements(PackageRelationshipsNamespace + "Relationship")
            .FirstOrDefault(element => string.Equals(
                (string?)element.Attribute("Id"),
                relationshipId,
                StringComparison.OrdinalIgnoreCase))
            ?.Attribute("Target")
            ?.Value
            ?? throw new InvalidOperationException($"Worksheet relationship '{relationshipId}' was not found.");

        var worksheetEntryName = ResolveWorkbookRelationshipTarget(target);
        var worksheetEntry = archive.GetEntry(worksheetEntryName)
            ?? throw new InvalidOperationException($"Worksheet entry '{worksheetEntryName}' was not found.");

        return XDocument.Load(worksheetEntry.Open());
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedStringsEntry is null)
        {
            return [];
        }

        var sharedStrings = XDocument.Load(sharedStringsEntry.Open());
        return sharedStrings.Root?
            .Elements(SpreadsheetNamespace + "si")
            .Select(item => string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value)))
            .ToList() ?? [];
    }

    private static EmailHeader? FindEmailHeader(
        IReadOnlyList<XElement> rows,
        IReadOnlyList<string> sharedStrings)
    {
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            foreach (var cell in rows[rowIndex].Elements(SpreadsheetNamespace + "c"))
            {
                if (string.Equals(
                    GetCellValue(cell, sharedStrings).Trim(),
                    EmailColumnName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return new EmailHeader(rowIndex, GetCellColumnReference(cell));
                }
            }
        }

        return null;
    }

    private static string GetCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var cellType = (string?)cell.Attribute("t");
        if (cellType == "inlineStr")
        {
            return string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value));
        }

        var value = cell.Element(SpreadsheetNamespace + "v")?.Value ?? string.Empty;
        if (cellType == "s" &&
            int.TryParse(value, out var sharedStringIndex) &&
            sharedStringIndex >= 0 &&
            sharedStringIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedStringIndex];
        }

        return value;
    }

    private static string GetCellColumnReference(XElement cell)
    {
        var cellReference = (string?)cell.Attribute("r") ?? string.Empty;
        return string.Concat(cellReference.TakeWhile(char.IsLetter)).ToUpperInvariant();
    }

    private static string ResolveWorkbookRelationshipTarget(string target)
    {
        var normalizedTarget = target.Replace('\\', '/').TrimStart('/');
        return normalizedTarget.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
            ? normalizedTarget
            : $"xl/{normalizedTarget}";
    }

    private sealed record EmailHeader(int RowIndex, string ColumnReference);
}
