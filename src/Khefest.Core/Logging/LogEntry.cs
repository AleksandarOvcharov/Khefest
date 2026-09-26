using Khefest.Core.Errors;

namespace Khefest.Core.Logging;

/// <summary>
/// Immutable record representing a single structured log message.
/// </summary>
public sealed record LogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    KhefestSubsystem Subsystem,
    string Category,
    string Message,
    Exception? Exception = null,
    IReadOnlyDictionary<string, object?>? Properties = null)
{
    public override string ToString()
    {
        var exPart = Exception != null ? $" | Exception: {Exception.Message}" : string.Empty;
        return $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level,-5}] [{Subsystem}/{Category}] {Message}{exPart}";
    }
}
