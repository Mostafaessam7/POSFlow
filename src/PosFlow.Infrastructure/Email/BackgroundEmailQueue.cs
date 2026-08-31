using System.Threading.Channels;
using PosFlow.Application.Common;

namespace PosFlow.Infrastructure.Email;

/// <summary>
/// In-process email queue backed by a bounded <see cref="Channel{T}"/>.
/// </summary>
/// <remarks>
/// In-process on purpose. The alternative is a durable store (Hangfire, a real broker), and for the
/// one thing currently queued — a password-reset email — that is a database, a dashboard and a
/// dependency to keep patched, to protect a message the user can always request again by clicking
/// the same button. That trade is not worth taking yet.
///
/// The cost is stated rather than hidden: <b>a queued email is lost if the process stops before it
/// is sent.</b> For password reset that is a retry. Anything queued here that a user cannot simply
/// ask for again needs durable storage instead, and that is the point at which this should be
/// replaced rather than extended.
///
/// The channel is bounded. An unbounded one turns a wedged SMTP server into unbounded memory
/// growth, which takes down the till — the thing that must not stop — to protect an email.
/// </remarks>
public sealed class BackgroundEmailQueue : IBackgroundEmailQueue
{
    /// <summary>
    /// Generous for the real volume (password resets), small enough that a stuck sender cannot
    /// consume meaningful memory.
    /// </summary>
    private const int Capacity = 500;

    private readonly Channel<QueuedEmail> _channel = Channel.CreateBounded<QueuedEmail>(
        new BoundedChannelOptions(Capacity)
        {
            // Drop rather than block. Writers are request threads: making a checkout wait because
            // the mail queue is full would let an email outage stop sales.
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });

    public ChannelReader<QueuedEmail> Reader => _channel.Reader;

    public void Enqueue(QueuedEmail email)
    {
        ArgumentNullException.ThrowIfNull(email);

        // The return value is deliberately ignored: false means the queue was full and the message
        // was dropped. The caller is a request handler that must not fail because of it. The
        // background service logs what it processes; a dropped message shows up as a reset email
        // that never arrives, which the user resolves by asking for another.
        _channel.Writer.TryWrite(email);
    }
}
