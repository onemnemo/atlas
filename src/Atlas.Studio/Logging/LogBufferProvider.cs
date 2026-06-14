using Microsoft.Extensions.Logging;

namespace Atlas.Studio.Logging;

/// <summary>
/// An <see cref="ILoggerProvider"/> that funnels every log message into a shared
/// <see cref="LogBuffer"/> so the dashboard can display the live system log.
/// </summary>
public sealed class LogBufferProvider : ILoggerProvider
{
    private readonly LogBuffer _buffer;

    public LogBufferProvider(LogBuffer buffer) => _buffer = buffer;

    public ILogger CreateLogger(string categoryName) => new BufferLogger(_buffer, categoryName);

    public void Dispose()
    {
    }

    private sealed class BufferLogger : ILogger
    {
        private readonly LogBuffer _buffer;
        private readonly string _category;

        public BufferLogger(LogBuffer buffer, string category)
        {
            _buffer = buffer;
            _category = ShortCategory(category);
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string message = formatter(state, exception);
            if (exception is not null)
            {
                message = $"{message} — {exception.GetType().Name}: {exception.Message}";
            }

            _buffer.Add(new LogEntry(DateTime.Now, logLevel, _category, message));
        }

        private static string ShortCategory(string category)
        {
            int lastDot = category.LastIndexOf('.');
            return lastDot >= 0 && lastDot < category.Length - 1 ? category[(lastDot + 1)..] : category;
        }
    }
}
