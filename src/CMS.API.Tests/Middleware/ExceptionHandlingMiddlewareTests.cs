using System.Text;
using System.Text.Json;
using CMS.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace CMS.API.Tests.Middleware;

/// <summary>
/// The middleware on its own, driven through a <see cref="DefaultHttpContext"/>.
///
/// Two of its rules can only be pinned here. A 403 is not something any endpoint in this API
/// produces today — there are no role policies, only the authenticated-user fallback — so the
/// promise that a deliberate 403 travels out untouched has to be made against a next-delegate that
/// sets one. A response whose first bytes have already gone out cannot be reproduced through the
/// pipeline either. <see cref="ExceptionHandlingPipelineTests"/> covers what the real host shows.
/// </summary>
public class ExceptionHandlingMiddlewareTests
{
    private static readonly InvalidOperationException Thrown = new(
        "Invalid column name 'RoleNam'. SELECT pkid FROM dbo.SecretRoleTable; server=db-prod-01");

    private sealed record Run(HttpContext Context, string Body, RecordingLogger Logger);

    /// <summary>Runs the middleware over a request whose inner delegate does whatever is asked.</summary>
    private static async Task<Run> InvokeAsync(RequestDelegate next, HttpContext? context = null)
    {
        context ??= new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;

        var logger = new RecordingLogger();
        await new ExceptionHandlingMiddleware(next, logger).InvokeAsync(context);

        return new Run(context, Encoding.UTF8.GetString(body.ToArray()), logger);
    }

    // ---------- Responses that are already meaningful pass straight through ----------

    [Theory]
    [InlineData(StatusCodes.Status401Unauthorized)]
    [InlineData(StatusCodes.Status403Forbidden)]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status404NotFound)]
    [InlineData(StatusCodes.Status409Conflict)]
    public async Task ADeliberateStatus_IsLeftExactlyAsItWas(int status)
    {
        const string answered = "{\"title\":\"as answered\"}";

        var run = await InvokeAsync(async context =>
        {
            context.Response.StatusCode = status;
            await context.Response.WriteAsync(answered);
        });

        Assert.Equal(status, run.Context.Response.StatusCode);
        Assert.Equal(answered, run.Body);
        Assert.Empty(run.Logger.Entries);
    }

    [Fact]
    public async Task ASuccessfulRequest_IsLeftExactlyAsItWas()
    {
        var run = await InvokeAsync(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync("[]");
        });

        Assert.Equal(StatusCodes.Status200OK, run.Context.Response.StatusCode);
        Assert.Equal("[]", run.Body);
    }

    // ---------- An unhandled exception becomes one fixed 500 ----------

    [Fact]
    public async Task AnUnhandledException_Answers500()
    {
        var run = await InvokeAsync(_ => throw Thrown);

        Assert.Equal(StatusCodes.Status500InternalServerError, run.Context.Response.StatusCode);
    }

    [Fact]
    public async Task AnUnhandledException_AnswersTheGenericProblemDetails()
    {
        var run = await InvokeAsync(_ => throw Thrown);

        var problem = JsonDocument.Parse(run.Body).RootElement;
        Assert.Equal(ExceptionHandlingMiddleware.GenericTitle, problem.GetProperty("title").GetString());
        Assert.Equal(ExceptionHandlingMiddleware.GenericDetail, problem.GetProperty("detail").GetString());
        Assert.Equal(500, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task AnUnhandledException_AnswersAsProblemJson()
    {
        var run = await InvokeAsync(_ => throw Thrown);

        Assert.Equal(ExceptionHandlingMiddleware.ProblemContentType, run.Context.Response.ContentType);
    }

    [Fact]
    public async Task AnUnhandledException_LeaksNothingFromTheException()
    {
        var run = await InvokeAsync(_ => throw Thrown);

        string[] secrets =
        [
            "SecretRoleTable", "db-prod-01", "SELECT", "Invalid column name",
            "InvalidOperationException", "stackTrace", "   at ",
        ];

        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(secret, run.Body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task AnUnhandledException_IsLoggedWithItsMessageAndStackTrace()
    {
        // A stack trace only exists on an exception that was actually thrown.
        Exception thrown;
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (InvalidOperationException caught)
        {
            thrown = caught;
        }

        var run = await InvokeAsync(_ => throw thrown);

        var entry = Assert.Single(run.Logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(thrown, entry.Exception);
        Assert.Equal("boom", entry.Exception!.Message);
        Assert.NotNull(entry.Exception.StackTrace);
    }

    // ---------- Headers already on the response survive ----------

    [Fact]
    public async Task AnUnhandledException_KeepsHeadersSetBeforeIt()
    {
        // CORS applies its headers before calling the rest of the pipeline. Clearing the response
        // would take them with it, and the browser would hand the Angular interceptor a status-0
        // network error instead of the 500 whose body carries the message.
        var run = await InvokeAsync(
            _ => throw Thrown,
            ContextWith("Access-Control-Allow-Origin", "http://localhost:4200"));

        Assert.Equal(
            "http://localhost:4200",
            run.Context.Response.Headers["Access-Control-Allow-Origin"].ToString());
    }

    private static DefaultHttpContext ContextWith(string header, StringValues value)
    {
        var context = new DefaultHttpContext();
        context.Response.Headers[header] = value;
        return context;
    }

    // ---------- A response already on the wire cannot be rewritten ----------

    [Fact]
    public async Task AnExceptionAfterTheResponseStarted_IsLoggedAndRethrown()
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());

        var logger = new RecordingLogger();
        var middleware = new ExceptionHandlingMiddleware(_ => throw Thrown, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
        Assert.Single(logger.Entries);
    }

    // ---------- A client that hung up is not a server error ----------

    [Fact]
    public async Task ARequestTheClientAborted_IsNotReportedAsAFailure()
    {
        var context = new DefaultHttpContext();
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        context.RequestAborted = aborted.Token;

        var run = await InvokeAsync(_ => throw new OperationCanceledException(aborted.Token), context);

        Assert.Equal(499, run.Context.Response.StatusCode);
        Assert.Equal(string.Empty, run.Body);
        Assert.Equal(LogLevel.Information, Assert.Single(run.Logger.Entries).Level);
    }

    [Fact]
    public async Task ACancellationTheClientDidNotCause_IsStillA500()
    {
        // Nothing aborted the request, so an OperationCanceledException is an ordinary bug.
        var run = await InvokeAsync(_ => throw new OperationCanceledException("a timeout somewhere"));

        Assert.Equal(StatusCodes.Status500InternalServerError, run.Context.Response.StatusCode);
        Assert.Contains(ExceptionHandlingMiddleware.GenericDetail, run.Body);
    }

    // ---------- Test doubles ----------

    private sealed record LogEntry(LogLevel Level, Exception? Exception);

    /// <summary>Captures what the middleware logged, so the server-side half can be asserted.</summary>
    private sealed class RecordingLogger : ILogger<ExceptionHandlingMiddleware>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add(new LogEntry(logLevel, exception));
    }

    /// <summary>A response whose first bytes have gone out. DefaultHttpContext's own never has.</summary>
    private sealed class StartedResponseFeature : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted => true;

        public void OnStarting(Func<object, Task> callback, object state) { }

        public void OnCompleted(Func<object, Task> callback, object state) { }
    }
}
