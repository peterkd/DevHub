namespace EmailComposer.Backend.Models;

public sealed class InactivityAlertSendResult
{
    public int RecipientCount { get; set; }

    public int SentCount { get; set; }
}
