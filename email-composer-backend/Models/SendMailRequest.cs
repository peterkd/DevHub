using System.ComponentModel.DataAnnotations;

namespace EmailComposer.Backend.Models;

public sealed class SendMailRequest
{
    [Required]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string BodyHtml { get; set; } = string.Empty;

    public List<string> ToRecipients { get; set; } = [];

    public bool IncludeSqlRecipients { get; set; }

    public string? OrganizationRole { get; set; }

    public string? UserRole { get; set; }

    public string? WorkerFunction { get; set; }

    public string? Position { get; set; }
}
