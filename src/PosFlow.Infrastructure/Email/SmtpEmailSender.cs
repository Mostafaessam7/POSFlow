using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using PosFlow.Application.Common;

namespace PosFlow.Infrastructure.Email;

/// <summary>
/// Real email delivery via SMTP (works with SendGrid, SES, Mailgun,
/// Office365, Gmail relay, or any standard SMTP server - just fill in
/// the Smtp:* settings via configuration/environment variables, never
/// committed values). Registered instead of LoggingEmailSender
/// whenever Smtp:Host is configured - see Program.cs.
/// </summary>
public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger)
    : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;
    private readonly ILogger<SmtpEmailSender> _logger = logger;

    public async Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(
            _options.FromName,
            _options.FromAddress));

        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = body };

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(
                _options.Host,
                _options.Port,
                _options.UseSsl
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.None,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                await client.AuthenticateAsync(
                    _options.Username,
                    _options.Password,
                    cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            // Never let a broken mail provider surface a 500 with
            // internal SMTP details to the caller, and never let it
            // block e.g. forgot-password from returning its
            // intentionally generic "if that account exists..."
            // response. Log it loudly instead so ops can see delivery
            // is broken.
            _logger.LogError(
                ex,
                "Failed to send email to {ToEmail} via SMTP {Host}:{Port}",
                toEmail,
                _options.Host,
                _options.Port);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, cancellationToken);
            }
        }
    }
}
