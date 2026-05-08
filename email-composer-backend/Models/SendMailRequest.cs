using System.ComponentModel.DataAnnotations;

namespace EmailComposer.Backend.Models;

public sealed class SendMailRequest
{
    [Required]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string BodyHtml { get; set; } = string.Empty;
}
