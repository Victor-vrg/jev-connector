using Microsoft.Extensions.Logging;

namespace JevConnector.ShimSmokeTests;

/// <summary>Minimal ILogger that writes warnings to the console, for the smoke test run.</summary>
internal sealed class ConsoleLogger : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Console.WriteLine($"      [{logLevel}] {formatter(state, exception)}");
    }
}
