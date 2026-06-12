using System.ComponentModel.DataAnnotations;

namespace EmailComposer.Backend.Models;

public sealed class MailAttachment
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string ContentType { get; set; } = "application/octet-stream";

    [Required]
    public string ContentBytes { get; set; } = string.Empty;
}
