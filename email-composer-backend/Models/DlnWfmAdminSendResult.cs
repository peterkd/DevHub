namespace EmailComposer.Backend.Models;

public sealed class DlnWfmAdminSendResult
{
    public int CompaniesWithActiveWfmAdminCount { get; set; }

    public int MatchedRecipientCount { get; set; }

    public int SentCount { get; set; }

    public List<string> SkippedCompanies { get; set; } = [];
}
