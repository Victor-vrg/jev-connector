// ---------------------------------------------------------------------------
// LOCAL BUILD/TEST SHIM for Microsoft.Extensions.Logging.Abstractions — NOT
// the real package. Reproduces just enough of the public surface (ILogger,
// LogLevel, EventId, the LogWarning extension overloads) to compile the
// connector in this sandbox, which has no access to nuget.org. The real
// OutSystems.JevConnector.csproj references the genuine NuGet package.
// ---------------------------------------------------------------------------

namespace Microsoft.Extensions.Logging;

public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    None = 6,
}

public readonly struct EventId
{
    public EventId(int id, string? name = null)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; }
    public string? Name { get; }

    public static implicit operator EventId(int id) => new(id);
}

public interface ILogger
{
    IDisposable? BeginScope<TState>(TState state) where TState : notnull;
    bool IsEnabled(LogLevel logLevel);
    void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter);
}

public static class LoggerExtensions
{
    public static void LogWarning(this ILogger logger, string message, params object?[] args)
        => Write(logger, LogLevel.Warning, null, message, args);

    public static void LogWarning(this ILogger logger, Exception? exception, string message, params object?[] args)
        => Write(logger, LogLevel.Warning, exception, message, args);

    private static void Write(ILogger logger, LogLevel level, Exception? exception, string message, object?[] args)
    {
        if (!logger.IsEnabled(level))
        {
            return;
        }
        logger.Log(level, default, message, exception, (m, _) => Format(m, args));
    }

    private static string Format(string message, object?[] args)
    {
        // Minimal structured-log-message formatter: replaces {Name} placeholders
        // positionally, which is all this project's log calls need.
        var result = message;
        foreach (var arg in args)
        {
            var start = result.IndexOf('{');
            var end = start >= 0 ? result.IndexOf('}', start) : -1;
            if (start < 0 || end < 0)
            {
                break;
            }
            result = string.Concat(result.AsSpan(0, start), arg?.ToString() ?? "null", result.AsSpan(end + 1));
        }
        return result;
    }
}
