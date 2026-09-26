using System.Collections.Concurrent;
using Khefest.Core.Errors;

namespace Khefest.Core.Logging;

/// <summary>
/// Central management facility for loggers, minimum severity filtering, and sink routing.
/// </summary>
public static class LogManager
{
    private static readonly List<ILogSink> Sinks = new();
    private static readonly object SyncLock = new();
    private static readonly ConcurrentDictionary<(KhefestSubsystem, string), ILogger> Loggers = new();
    private static readonly ConcurrentDictionary<KhefestSubsystem, LogLevel> SubsystemLevels = new();

    public static LogLevel GlobalMinimumLevel { get; set; } = LogLevel.Info;

    static LogManager()
    {
        // Default to console sink
        AddSink(new ConsoleLogSink());
    }

    public static void AddSink(ILogSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        lock (SyncLock)
        {
            if (!Sinks.Contains(sink))
            {
                Sinks.Add(sink);
            }
        }
    }

    public static void RemoveSink(ILogSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        lock (SyncLock)
        {
            Sinks.Remove(sink);
        }
    }

    public static void ClearSinks()
    {
        lock (SyncLock)
        {
            Sinks.Clear();
        }
    }

    public static void SetSubsystemLevel(KhefestSubsystem subsystem, LogLevel level)
    {
        SubsystemLevels[subsystem] = level;
    }

    public static LogLevel GetEffectiveLevel(KhefestSubsystem subsystem)
    {
        if (SubsystemLevels.TryGetValue(subsystem, out var level))
        {
            return level;
        }

        return GlobalMinimumLevel;
    }

    public static ILogger GetLogger(KhefestSubsystem subsystem, string category = "")
    {
        return Loggers.GetOrAdd((subsystem, category), key => new DefaultLogger(key.Item1, key.Item2));
    }

    public static ILogger GetLogger<T>(KhefestSubsystem subsystem)
    {
        return GetLogger(subsystem, typeof(T).Name);
    }

    internal static void Dispatch(in LogEntry entry)
    {
        ILogSink[] sinksCopy;
        lock (SyncLock)
        {
            sinksCopy = Sinks.ToArray();
        }

        foreach (var sink in sinksCopy)
        {
            try
            {
                sink.Emit(entry);
            }
            catch
            {
                // Never allow a failing sink to crash the application loop
            }
        }
    }

    private sealed class DefaultLogger : ILogger
    {
        public KhefestSubsystem Subsystem { get; }
        public string Category { get; }

        public DefaultLogger(KhefestSubsystem subsystem, string category)
        {
            Subsystem = subsystem;
            Category = string.IsNullOrWhiteSpace(category) ? subsystem.ToString() : category;
        }

        public bool IsEnabled(LogLevel level)
        {
            return level >= GetEffectiveLevel(Subsystem);
        }

        public void Log(LogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null)
        {
            if (!IsEnabled(level))
            {
                return;
            }

            var entry = new LogEntry(
                DateTimeOffset.UtcNow,
                level,
                Subsystem,
                Category,
                message,
                exception,
                properties);

            Dispatch(entry);
        }
    }
}
