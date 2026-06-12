using EmailComposer.Backend.Models;

namespace EmailComposer.Backend.Services;

public sealed class DlnWfmAdminMailService
{
    private const string CompaniesWithWfmAdminDirectoryPath = @"C:\WFM\DLN\Import\CompaniesWithWFMAdmin";

    private static readonly IReadOnlyDictionary<string, string> ContentTypesByExtension =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".csv"] = "text/csv",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".pdf"] = "application/pdf",
            [".txt"] = "text/plain",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".zip"] = "application/zip"
        };

    private readonly SqlRecipientService _sqlRecipientService;
    private readonly GraphMailService _graphMailService;

    public DlnWfmAdminMailService(
        SqlRecipientService sqlRecipientService,
        GraphMailService graphMailService)
    {
        _sqlRecipientService = sqlRecipientService;
        _graphMailService = graphMailService;
    }

    public async Task<DlnWfmAdminSendResult> SendAsync(
        DlnWfmAdminSendRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.BodyHtml))
        {
            throw new InvalidOperationException("Subject and email body are required.");
        }

        if (!Directory.Exists(CompaniesWithWfmAdminDirectoryPath))
        {
            throw new InvalidOperationException(
                $"DLN import directory was not found: {CompaniesWithWfmAdminDirectoryPath}");
        }

        var companyFiles = Directory.EnumerateFiles(CompaniesWithWfmAdminDirectoryPath)
            .Select(filePath => new CompanyFile(Path.GetFileNameWithoutExtension(filePath).Trim(), filePath))
            .Where(file => file.CompanyName.Length > 0)
            .GroupBy(file => file.CompanyName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().FilePath, StringComparer.OrdinalIgnoreCase);

        if (companyFiles.Count == 0)
        {
            throw new InvalidOperationException(
                $"No DLN import files were found in {CompaniesWithWfmAdminDirectoryPath}.");
        }

        var companiesWithActiveWfmAdmins =
            await _sqlRecipientService.GetCompanyActiveWfmAdminsAsync(cancellationToken);
        var dlnWfmAdminMailRecipients = companiesWithActiveWfmAdmins
            .Where(recipient => companyFiles.ContainsKey(recipient.CompanyName))
            .ToList();

        var result = new DlnWfmAdminSendResult
        {
            CompaniesWithActiveWfmAdminCount = companiesWithActiveWfmAdmins.Count,
            MatchedRecipientCount = dlnWfmAdminMailRecipients.Count,
            SkippedCompanies = companyFiles.Keys
                .Except(dlnWfmAdminMailRecipients.Select(recipient => recipient.CompanyName), StringComparer.OrdinalIgnoreCase)
                .OrderBy(companyName => companyName, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        foreach (var recipient in dlnWfmAdminMailRecipients)
        {
            var filePath = companyFiles[recipient.CompanyName];
            var attachment = new MailAttachment
            {
                Name = Path.GetFileName(filePath),
                ContentType = GetContentType(filePath),
                ContentBytes = Convert.ToBase64String(await File.ReadAllBytesAsync(filePath, cancellationToken))
            };

            await _graphMailService.SendMailAsync(
                new SendMailRequest
                {
                    Subject = request.Subject,
                    BodyHtml = request.BodyHtml,
                    ToRecipients = recipient.EmailAddresses.ToList(),
                    Attachments = [attachment]
                },
                cancellationToken);

            result.SentCount++;
        }

        return result;
    }

    private static string GetContentType(string filePath)
    {
        return ContentTypesByExtension.TryGetValue(Path.GetExtension(filePath), out var contentType)
            ? contentType
            : "application/octet-stream";
    }

    private sealed record CompanyFile(string CompanyName, string FilePath);
}
