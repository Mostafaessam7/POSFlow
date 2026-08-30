using Serilog.Context;

namespace PosFlow.Api.Middleware;

/// <summary>
/// Gives every request a correlation id, echoes it back on the response, and puts it into the
/// Serilog context so every log line written while handling that request carries it.
/// </summary>
/// <remarks>
/// The gap this closes: when a cashier reports "it failed", the only way to find the matching log
/// lines was to guess from a timestamp. On a busy till with several terminals against one API that
/// is not workable — the interleaved lines from concurrent requests cannot be told apart.
///
/// An incoming <c>X-Correlation-Id</c> is honoured so a caller can tie its own logs to ours, but
/// it is treated as untrusted: it lands in log output, so an unbounded or newline-carrying value
/// would let a caller forge log entries. It is length-capped and restricted to characters that
/// cannot break a log line, and anything failing that is replaced rather than rejected — a bad
/// header is not worth failing a sale over.
/// </remarks>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>A GUID with no dashes is 32; this leaves room for a caller's own scheme.</summary>
    private const int MaxLength = 64;

    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        // Echoed on the way out so the client can quote it in a bug report. Set via OnStarting
        // because headers cannot be written once the response has begun, and something further
        // down the pipeline may start it.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // TraceIdentifier flows into ProblemDetails responses, so an error body and the logs end
        // up quoting the same id rather than two unrelated ones.
        context.TraceIdentifier = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();

        return IsSafe(incoming) ? incoming! : Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Accepts only characters that cannot break a log line or smuggle content into one:
    /// letters, digits, dash and underscore. Rejecting newlines is the point — this value is
    /// written to the log, so permitting them would let a caller inject fabricated entries.
    /// </summary>
    private static bool IsSafe(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')
            {
                return false;
            }
        }

        return true;
    }
}
