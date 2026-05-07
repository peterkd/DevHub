namespace EmailComposer.Backend.Options;

public sealed class GraphOptions
{
    public const string SectionName = "Graph";

    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SenderUserId { get; set; } = string.Empty;
    public string Scope { get; set; } = "https://graph.microsoft.com/.default";
}
