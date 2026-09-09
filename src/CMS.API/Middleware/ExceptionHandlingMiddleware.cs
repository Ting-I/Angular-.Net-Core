using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Middleware;

/// <summary>
/// The one place an unhandled exception becomes a response.
///
/// Without it an exception escaping a controller or a repository leaves the pipeline with a bare
/// 500 and an empty body — nothing in the log that says what happened, and nothing on the wire the
/// Angular side can render. With it the exception is logged in full on the server and the caller
/// gets one fixed <see cref="ProblemDetails"/> shape that says only that something went wrong.
///
/// **It answers for the unexpected, and only that.** A 401 from the bearer middleware, a 403 from
/// authorization, a 400 from model validation and a 404 / 409 from a controller are all deliberate
/// answers, produced by returning a status rather than by throwing, so they never reach the
/// <c>catch</c> and travel out untouched.
///
/// Three details are load-bearing:
///
/// - **The response body carries no exception detail.** <see cref="GenericDetail"/> is a constant.
///   A stack trace names the internals, a <c>SqlException</c> message quotes the statement and
///   often the object names in it, and a connection failure quotes the server and database — all
///   of it a gift to somebody probing the API, and none of it actionable by an operator. The
///   server log is where it goes instead, and <c>traceId</c> is what ties the two together, so a
///   report of "it said 系統發生錯誤 at 14:32" can still be traced to the exception that caused it.
/// - **Headers already on the response are left alone.** <c>Response.Clear()</c> would take the
///   CORS headers with them, and a 500 the browser refuses to read as cross-origin reaches the
///   Angular interceptor as a status-0 network error rather than as the 500 it is — the toast
///   would lose the message this class exists to deliver. Only the status code and the body are
///   written.
/// - **A response already on the wire cannot be rewritten.** Once the first bytes have been
///   flushed the status line is gone, so the exception is logged and rethrown rather than
///   producing a body spliced onto a half-sent one.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    /// <summary>Operator-facing wording for an unexpected failure, per the house language split.</summary>
    public const string GenericTitle = "系統發生錯誤，請稍後再試。";

    /// <summary>The English sentence beside it. Fixed text — never anything from the exception.</summary>
    public const string GenericDetail = "An unexpected error occurred.";

    /// <summary>Content type of the error body, matching the ProblemDetails the controllers return.</summary>
    public const string ProblemContentType = "application/problem+json";

    /// <summary>
    /// A request the client gave up on. Nginx's convention, and not a 5xx: the server did not
    /// fail, so it must not be logged or reported as though it had.
    /// </summary>
    private const int ClientClosedRequest = 499;

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The caller hung up — every action takes a CancellationToken, so this is the normal
            // shape of an abandoned request rather than a fault. Nobody is left to read a body.
            _logger.LogInformation(
                "Request aborted by the client: {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = ClientClosedRequest;
            }
        }
        catch (Exception exception)
        {
            // Message and stack trace both — LogError(exception, …) writes the whole chain,
            // including any InnerException, which is where a Dapper failure keeps the real cause.
            _logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path} (traceId {TraceId})",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);

            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteProblemAsync(context);
        }
    }

    private static async Task WriteProblemAsync(HttpContext context)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = GenericTitle,
            Detail = GenericDetail,
            Instance = context.Request.Path,
        };

        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: ProblemContentType);
    }
}
