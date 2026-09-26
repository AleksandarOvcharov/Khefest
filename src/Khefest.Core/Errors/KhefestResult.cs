using System.Diagnostics.CodeAnalysis;

namespace Khefest.Core.Errors;

/// <summary>
/// Represents the outcome of an operation without throwing exceptions.
/// </summary>
public readonly struct KhefestResult
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public KhefestErrorCode ErrorCode { get; }
    public string? ErrorMessage { get; }
    public KhefestErrorContext? Context { get; }

    private KhefestResult(bool isSuccess, KhefestErrorCode errorCode, string? errorMessage, KhefestErrorContext? context)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        Context = context;
    }

    public static KhefestResult Success() => new(true, KhefestErrorCode.None, null, null);

    public static KhefestResult Failure(
        KhefestErrorCode errorCode,
        string errorMessage,
        KhefestErrorContext? context = null)
    {
        return new(false, errorCode, errorMessage, context);
    }

    public static KhefestResult Failure(
        KhefestSubsystem subsystem,
        KhefestErrorCode errorCode,
        string errorMessage)
    {
        var ctx = new KhefestErrorContext(subsystem, errorCode);
        return new(false, errorCode, errorMessage, ctx);
    }
}

/// <summary>
/// Represents the outcome of an operation that produces a value of type <typeparamref name="T"/> or an error.
/// </summary>
public readonly struct KhefestResult<T>
{
    private readonly T? _value;

    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;
    public KhefestErrorCode ErrorCode { get; }
    public string? ErrorMessage { get; }
    public KhefestErrorContext? Context { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value when KhefestResult is a failure. Error: {ErrorCode} - {ErrorMessage}");

    private KhefestResult(T? value, bool isSuccess, KhefestErrorCode errorCode, string? errorMessage, KhefestErrorContext? context)
    {
        _value = value;
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        Context = context;
    }

    public static KhefestResult<T> Success(T value) => new(value, true, KhefestErrorCode.None, null, null);

    public static KhefestResult<T> Failure(
        KhefestErrorCode errorCode,
        string errorMessage,
        KhefestErrorContext? context = null)
    {
        return new(default, false, errorCode, errorMessage, context);
    }

    public static KhefestResult<T> Failure(
        KhefestSubsystem subsystem,
        KhefestErrorCode errorCode,
        string errorMessage)
    {
        var ctx = new KhefestErrorContext(subsystem, errorCode);
        return new(default, false, errorCode, errorMessage, ctx);
    }

    public bool TryGetValue([NotNullWhen(true)] out T? value)
    {
        if (IsSuccess)
        {
            value = _value!;
            return true;
        }

        value = default;
        return false;
    }

    public static implicit operator KhefestResult<T>(T value) => Success(value);
}
