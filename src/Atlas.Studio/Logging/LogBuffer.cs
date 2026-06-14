using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Atlas.Studio.Logging;

/// <summary>One captured log line.</summary>
/// <param name="Timestamp">When it was logged.</param>
/// <param name="Level">The log level.</param>
/// <param name="Category">The logger category (usually the type name).</param>
/// <param name="Message">The formatted message.</param>
public readonly record struct LogEntry(DateTime Timestamp, LogLevel Level, string Category, string Message);

/// <summary>
/// A bounded, thread-safe ring buffer of recent log entries that the Logs screen
/// reads each frame. Logging happens on many threads; the UI reads on one — so
/// the buffer is the safe hand-off point.
/// </summary>
public sealed class LogBuffer
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private readonly int _capacity;

    public LogBuffer(int capacity = 2000) => _capacity = capacity;

    public void Add(LogEntry entry)
    {
        _entries.Enqueue(entry);
        while (_entries.Count > _capacity && _entries.TryDequeue(out _))
        {
            // Trim oldest beyond capacity.
        }
    }

    /// <summary>Returns a point-in-time copy of the buffered entries, oldest first.</summary>
    public IReadOnlyList<LogEntry> Snapshot() => [.. _entries];

    public void Clear()
    {
        while (_entries.TryDequeue(out _))
        {
            // Drain.
        }
    }
}
