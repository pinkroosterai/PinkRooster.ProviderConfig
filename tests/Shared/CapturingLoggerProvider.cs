using Microsoft.Extensions.Logging;

namespace PinkRooster.ProviderConfig.Tests.TestSupport;

/// <summary>An <see cref="ILoggerProvider"/> that keeps every log entry for assertions.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<LogEntry> _entries = [];

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_entries)
            {
                return [.. _entries];
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    public void Dispose()
    {
    }

    internal sealed record LogEntry(string Category, LogLevel Level, string Message);

    private sealed class CapturingLogger(CapturingLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (owner._entries)
            {
                owner._entries.Add(new LogEntry(category, logLevel, formatter(state, exception)));
            }
        }
    }
}
