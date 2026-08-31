using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PosFlow.Application.Common;
using PosFlow.Infrastructure.Email;
using Xunit;

namespace PosFlow.Application.Tests.Email;

/// <summary>
/// Covers the queue that moved password-reset email off the request thread.
/// </summary>
/// <remarks>
/// The point of the change was not speed. <c>ForgotPasswordAsync</c> returns early for an unknown
/// username so the response cannot distinguish a registered account from an unregistered one, and
/// sending inline undid that: the known-user path waited on SMTP while the unknown-user path
/// returned immediately, so the endpoint could be timed to enumerate usernames.
///
/// These assert the mechanism rather than the timing. A timing assertion would be flaky on a
/// shared runner and would prove less: what has to be true is that enqueueing does not touch the
/// sender at all, and that the message still gets delivered afterwards.
/// </remarks>
public class BackgroundEmailQueueTests
{
    [Fact]
    public void Enqueue_does_not_touch_the_sender()
    {
        // The property that closes the timing channel: whatever the sender does -- connect, hand
        // shake, block -- none of it happens on the caller's thread.
        var sender = new RecordingEmailSender();
        var queue = new BackgroundEmailQueue();

        queue.Enqueue(new QueuedEmail("someone@example.com", "subject", "body"));

        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task A_queued_email_is_delivered_by_the_background_service()
    {
        var sender = new RecordingEmailSender();
        var queue = new BackgroundEmailQueue();
        await using var provider = BuildProvider(sender);

        var service = new BackgroundEmailSenderService(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BackgroundEmailSenderService>.Instance);

        queue.Enqueue(new QueuedEmail("someone@example.com", "subject", "body"));

        await RunUntilAsync(service, () => sender.Sent.Count == 1);

        var sent = Assert.Single(sender.Sent);
        Assert.Equal("someone@example.com", sent.ToEmail);
        Assert.Equal("subject", sent.Subject);
    }

    [Fact]
    public async Task One_failing_email_does_not_stop_the_ones_behind_it()
    {
        // Without the try/catch in the service, the first failure ends ExecuteAsync and every later
        // email is queued and silently never sent, with nothing failing loudly. That is the worst
        // shape this could take: reset emails stop arriving and the logs say nothing.
        var sender = new RecordingEmailSender { ThrowOnSubject = "poison" };
        var queue = new BackgroundEmailQueue();
        await using var provider = BuildProvider(sender);

        var service = new BackgroundEmailSenderService(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BackgroundEmailSenderService>.Instance);

        queue.Enqueue(new QueuedEmail("a@example.com", "poison", "body"));
        queue.Enqueue(new QueuedEmail("b@example.com", "good", "body"));

        await RunUntilAsync(service, () => sender.Sent.Any(e => e.Subject == "good"));

        Assert.Contains(sender.Sent, e => e.Subject == "good");
    }

    [Fact]
    public void The_queue_drops_rather_than_blocking_when_it_is_full()
    {
        // Writers are request threads. A queue that blocked when full would let a wedged SMTP
        // server stall checkout -- taking down sales to protect an email.
        var queue = new BackgroundEmailQueue();

        var enqueueing = () =>
        {
            for (var i = 0; i < 2_000; i++)
            {
                queue.Enqueue(new QueuedEmail($"{i}@example.com", "subject", "body"));
            }
        };

        // Nothing reads the channel here, so this overflows capacity several times over. It has to
        // return rather than block or throw.
        var completed = Task.Run(enqueueing).Wait(TimeSpan.FromSeconds(5));

        Assert.True(completed, "Enqueue blocked when the queue was full; it must drop instead.");
    }

    private static ServiceProvider BuildProvider(IEmailSender sender)
    {
        var services = new ServiceCollection();

        // Scoped, matching the real registration -- the service must open a scope per message
        // rather than capture one sender for the process lifetime.
        services.AddScoped(_ => sender);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Runs the hosted service until <paramref name="done"/> holds, then stops it. Polls instead of
    /// sleeping a fixed interval so the test is not tuned to a machine's speed.
    /// </summary>
    private static async Task RunUntilAsync(BackgroundEmailSenderService service, Func<bool> done)
    {
        await service.StartAsync(CancellationToken.None);

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            while (!done() && !timeout.IsCancellationRequested)
            {
                await Task.Delay(10, CancellationToken.None);
            }
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        private readonly List<QueuedEmail> _sent = [];
        private readonly Lock _gate = new();

        public string? ThrowOnSubject { get; init; }

        public IReadOnlyList<QueuedEmail> Sent
        {
            get { lock (_gate) { return _sent.ToList(); } }
        }

        public Task SendAsync(
            string toEmail,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
        {
            if (subject == ThrowOnSubject)
            {
                throw new InvalidOperationException("Simulated delivery failure.");
            }

            lock (_gate) { _sent.Add(new QueuedEmail(toEmail, subject, body)); }

            return Task.CompletedTask;
        }
    }
}
