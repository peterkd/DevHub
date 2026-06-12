namespace EmailComposer.Backend.Models;

public sealed record CompanyWfmAdminRecipient(
    string CompanyName,
    IReadOnlyList<string> EmailAddresses);
