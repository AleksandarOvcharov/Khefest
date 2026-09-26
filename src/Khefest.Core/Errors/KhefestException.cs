using System.Text;

namespace Khefest.Core.Errors;

/// <summary>
/// Root exception type for all Khefest framework errors.
/// Provides structured subsystem metadata, error codes, and diagnostic context.
/// </summary>
public class KhefestException : Exception
{
    public KhefestSubsystem Subsystem { get; }
    public KhefestErrorCode ErrorCode { get; }
    public KhefestErrorContext Context { get; }

    public KhefestException(
        KhefestSubsystem subsystem,
        KhefestErrorCode errorCode,
        string message,
        Exception? innerException = null,
        KhefestErrorContext? context = null)
        : base(FormatMessage(subsystem, errorCode, message, context), innerException)
    {
        Subsystem = subsystem;
        ErrorCode = errorCode;
        Context = context ?? new KhefestErrorContext(subsystem, errorCode);
    }

    private static string FormatMessage(
        KhefestSubsystem subsystem,
        KhefestErrorCode errorCode,
        string message,
        KhefestErrorContext? context)
    {
        var sb = new StringBuilder();
        sb.Append($"[{subsystem}] Error {errorCode} ({(int)errorCode}): {message}");
        if (context != null && context.Properties.Count > 0)
        {
            sb.Append(" | Details: { ");
            sb.Append(string.Join(", ", context.Properties.Select(p => $"{p.Key} = '{p.Value}'")));
            sb.Append(" }");
        }
        return sb.ToString();
    }
}
