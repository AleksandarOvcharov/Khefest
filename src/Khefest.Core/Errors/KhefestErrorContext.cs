using System.Collections.Concurrent;
using System.Text;

namespace Khefest.Core.Errors;

/// <summary>
/// Provides rich diagnostic context for errors and exceptions within Khefest.
/// </summary>
public sealed class KhefestErrorContext
{
    private readonly ConcurrentDictionary<string, object?> _properties = new();

    public KhefestSubsystem Subsystem { get; }
    public KhefestErrorCode ErrorCode { get; }
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    public int ThreadId { get; } = Environment.CurrentManagedThreadId;
    public int? NativeErrorCode { get; init; }

    public KhefestErrorContext(KhefestSubsystem subsystem, KhefestErrorCode errorCode)
    {
        Subsystem = subsystem;
        ErrorCode = errorCode;
    }

    public IReadOnlyDictionary<string, object?> Properties => _properties;

    public KhefestErrorContext With(string key, object? value)
    {
        _properties[key] = value;
        return this;
    }

    public KhefestErrorContext WithNativeError(int nativeErrorCode)
    {
        return new KhefestErrorContext(Subsystem, ErrorCode)
        {
            NativeErrorCode = nativeErrorCode
        };
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append($"[Subsystem: {Subsystem}, Code: {ErrorCode} ({(int)ErrorCode})");
        if (NativeErrorCode.HasValue)
        {
            sb.Append($", NativeError: 0x{NativeErrorCode.Value:X8} ({NativeErrorCode.Value})");
        }
        sb.Append($", Thread: {ThreadId}, Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fff}]");

        if (!_properties.IsEmpty)
        {
            sb.Append(" Context: { ");
            sb.Append(string.Join(", ", _properties.Select(kv => $"{kv.Key} = '{kv.Value}'")));
            sb.Append(" }");
        }

        return sb.ToString();
    }
}
