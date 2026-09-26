using Khefest.Core.Errors;

namespace Khefest.Core.Logging;

/// <summary>
/// Subsystem-specific logger interface.
/// </summary>
public interface ILogger
{
    KhefestSubsystem Subsystem { get; }
    string Category { get; }

    bool IsEnabled(LogLevel level);

    void Log(LogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null);

    void Trace(string message) => Log(LogLevel.Trace, message);
    void Debug(string message) => Log(LogLevel.Debug, message);
    void Info(string message) => Log(LogLevel.Info, message);
    void Warning(string message, Exception? exception = null) => Log(LogLevel.Warning, message, exception);
    void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);
    void Fatal(string message, Exception? exception = null) => Log(LogLevel.Fatal, message, exception);
}
