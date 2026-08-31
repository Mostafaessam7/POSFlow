using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PosFlow.Application.Common;

namespace PosFlow.Infrastructure.Email;

/// <summary>
/// Drains <see cref="BackgroundEmailQueue"/> and delivers each message through
/// <see cref="IEmailSender"/>.
/// </summary>
/// <remarks>
/// Resolves the sender from a fresh scope per message. <see cref="IEmailSender"/> is registered
/// scoped, and a hosted service is a singleton — capturing a scoped dependency in one is the
/// classic captive-dependency bug, and it would hold a single SMTP client for the process lifetime.
/// </remarks>
public sealed class BackgroundEmailSenderService(
    BackgroundEmailQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<BackgroundEmailSenderService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Background email sender started.");

        await foreach (var email in queue.Reader.ReadAllAsync(stoppingToken))
        {
            await SendOneAsync(email, stoppingToken);
        }
    }

    private async Task SendOneAsync(QueuedEmail email, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            await sender.SendAsync(email.ToEmail, email.Subject, email.Body, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down. Not a failure, and rethrowing would log a spurious error on every stop.
            throw;
        }
        catch (Exception ex)
        {
            // One bad message must not kill the loop: the exception would end ExecuteAsync and every
            // later email would be queued and silently never sent, with nothing failing loudly.
            //
            // The recipient address is not logged. These are password-reset mails, so the address
            // plus the timing is enough to tell that a particular person has an account here.
            logger.LogError(
                ex,
                "Failed to deliver a queued email with subject {Subject}. It is not retried.",
                email.Subject);
        }
    }
}
