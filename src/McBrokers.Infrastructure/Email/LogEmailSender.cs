using McBrokers.Application.Ports;
using Microsoft.Extensions.Logging;

namespace McBrokers.Infrastructure.Email;

/// <summary>
/// Impl de desarrollo: sólo loguea. En producción usar Microsoft Graph (cuenta institucional)
/// o SendGrid (provisioning en INFRA_AZURE.md, pendiente).
/// </summary>
public sealed class LogEmailSender : IEmailSender
{
    private readonly ILogger<LogEmailSender> _logger;

    public LogEmailSender(ILogger<LogEmailSender> logger) => _logger = logger;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[DEV email] To={To} Subject='{Subject}' Attachments={Count}",
            message.ToAddress, message.Subject, message.Attachments.Count);
        return Task.CompletedTask;
    }
}
