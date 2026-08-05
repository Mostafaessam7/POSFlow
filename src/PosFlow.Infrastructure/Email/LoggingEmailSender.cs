using Microsoft.Extensions.Logging;
using PosFlow.Application.Common;

namespace PosFlow.Infrastructure.Email;

/// <summary>
/// TEMPORARY default: logs the email instead of sending it. This lets
/// forgot-password work end-to-end in development (grab the reset
/// link from the logs) without requiring real email infrastructure.
///
/// Before going to production, replace the DI registration of
/// IEmailSender in Program.cs with a real provider (SendGrid, SES,
/// SMTP via MailKit, etc.) - this class should never run in
/// production as-is, since nobody actually receives the email.
/// </summary>
public sealed class LoggingEmailSender(
    ILogger<LoggingEmailSender> logger)
    : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger = logger;

    public Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[DEV EMAIL - no real provider configured] To: {ToEmail} | Subject: {Subject}\n{Body}",
            toEmail,
            subject,
            body);

        return Task.CompletedTask;
    }
}
