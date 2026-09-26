using System.Collections.Concurrent;

namespace Khefest.Core.Logging;

/// <summary>
/// Destination for formatted log entries.
/// </summary>
public interface ILogSink
{
    void Emit(in LogEntry entry);
}

/// <summary>
/// Writes log entries to standard output or error stream with formatted severity tags.
/// </summary>
public sealed class ConsoleLogSink : ILogSink
{
    private readonly object _lock = new();

    public void Emit(in LogEntry entry)
    {
        lock (_lock)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = entry.Level switch
            {
                LogLevel.Trace => ConsoleColor.DarkGray,
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Info => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Fatal => ConsoleColor.Magenta,
                _ => ConsoleColor.White
            };

            Console.WriteLine(entry.ToString());
            if (entry.Exception != null)
            {
                Console.WriteLine(entry.Exception.StackTrace);
            }

            Console.ForegroundColor = originalColor;
        }
    }
}

/// <summary>
/// Retains the most recent log entries in memory for in-game debug displays and diagnostics.
/// </summary>
public sealed class MemoryLogSink : ILogSink
{
    private readonly ConcurrentQueue<LogEntry> _queue = new();
    private readonly int _maxCapacity;

    public MemoryLogSink(int maxCapacity = 1000)
    {
        _maxCapacity = Math.Max(10, maxCapacity);
    }

    public void Emit(in LogEntry entry)
    {
        _queue.Enqueue(entry);
        while (_queue.Count > _maxCapacity && _queue.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<LogEntry> GetRecentEntries() => _queue.ToArray();

    public void Clear() => _queue.Clear();
}

/// <summary>
/// Routes log entries to a delegate or action.
/// </summary>
public sealed class CallbackLogSink : ILogSink
{
    private readonly Action<LogEntry> _callback;

    public CallbackLogSink(Action<LogEntry> callback)
    {
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    public void Emit(in LogEntry entry) => _callback(entry);
}
