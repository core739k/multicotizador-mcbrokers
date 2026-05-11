namespace McBrokers.Application.Ports;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed record EmailMessage(
    string ToAddress,
    string Subject,
    string BodyHtml,
    IReadOnlyList<EmailAttachment> Attachments);

public sealed record EmailAttachment(
    string FileName,
    string ContentType,
    string BlobRef);
