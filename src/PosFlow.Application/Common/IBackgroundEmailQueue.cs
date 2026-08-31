namespace PosFlow.Application.Common;

/// <summary>
/// Accepts an email for delivery after the current request has finished.
/// </summary>
/// <remarks>
/// Introduced for forgot-password, where sending inline was not just slow but leaked the thing the
/// endpoint is written to hide. <c>ForgotPasswordAsync</c> deliberately no-ops for an unknown
/// username so the response cannot be used to tell registered accounts from unregistered ones — but
/// the known-user path then opened an SMTP connection and waited for it, while the unknown-user
/// path returned straight after a single query. Seconds against milliseconds is a difference a
/// caller can measure, so the endpoint enumerated usernames by timing regardless of the identical
/// response body.
///
/// Queueing removes the dominant part of that difference. It does not make the two paths
/// constant-time — the known-user path still writes a token row — but it takes the gap from
/// "however long SMTP takes" down to a database insert, which is the difference between a signal
/// you can measure over the internet and one you cannot.
/// </remarks>
public interface IBackgroundEmailQueue
{
    /// <summary>
    /// Queues an email and returns immediately. Never throws for delivery problems: those happen
    /// later, on a background thread, and are logged there.
    /// </summary>
    void Enqueue(QueuedEmail email);
}

/// <param name="ToEmail">Recipient address.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="Body">Plain-text body.</param>
public sealed record QueuedEmail(string ToEmail, string Subject, string Body);
