using Microsoft.Extensions.Logging;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// An <see cref="ILogger{TCategoryName}"/> that keeps what it was handed, so a test can assert the
/// log line an endpoint exists to emit — including its *structure*, not just its rendered text.
///
/// Hand-written for the same reason every repository stand-in here is: the test project takes no
/// mocking or diagnostics-testing package, and a logger is twenty lines.
/// </summary>
public class CapturingLogger<TCategoryName> : ILogger<TCategoryName>
{
    /// <summary>One captured call: its level, its rendered message, and its named values.</summary>
    public record Entry(LogLevel Level, string Message, IReadOnlyDictionary<string, object?> State)
    {
        /// <summary>The value of one placeholder, or null when the entry does not carry it.</summary>
        public object? Value(string name) => State.GetValueOrDefault(name);

        /// <summary>The value of one placeholder as text — what a formatter would render.</summary>
        public string? Text(string name) => Value(name)?.ToString();
    }

    public List<Entry> Entries { get; } = [];

    /// <summary>The only entry, asserting there is exactly one.</summary>
    public Entry Single(LogLevel level)
    {
        var entry = Assert.Single(Entries);
        Assert.Equal(level, entry.Level);
        return entry;
    }

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
        => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var values = new Dictionary<string, object?>();
        if (state is IReadOnlyList<KeyValuePair<string, object?>> pairs)
        {
            foreach (var pair in pairs)
            {
                values[pair.Key] = pair.Value;
            }
        }

        Entries.Add(new Entry(logLevel, formatter(state, exception), values));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
