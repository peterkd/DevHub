using System.ComponentModel.DataAnnotations;

namespace EmailComposer.Backend.Models;

public sealed class SendMailRequest : IValidatableObject
{
    [Required]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string BodyHtml { get; set; } = string.Empty;

    public List<string> ToRecipients { get; set; } = [];

    public bool IncludeSqlRecipients { get; set; }

    public string? OrganizationRole { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasManualRecipients = (ToRecipients ?? []).Any(email => !string.IsNullOrWhiteSpace(email));

        if (!IncludeSqlRecipients && !hasManualRecipients)
        {
            yield return new ValidationResult(
                "At least one recipient is required when SQL recipients are not included.",
                [nameof(ToRecipients)]);
        }

        if (IncludeSqlRecipients && string.IsNullOrWhiteSpace(OrganizationRole))
        {
            yield return new ValidationResult(
                "OrganizationRole is required when SQL recipients are included.",
                [nameof(OrganizationRole)]);
        }
    }
}
