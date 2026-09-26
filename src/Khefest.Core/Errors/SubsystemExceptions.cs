namespace Khefest.Core.Errors;

/// <summary>
/// Exception thrown during graphics device, pipeline, shader, or presentation operations.
/// </summary>
public sealed class KhefestGraphicsException : KhefestException
{
    public KhefestGraphicsException(
        KhefestErrorCode errorCode,
        string message,
        Exception? innerException = null,
        KhefestErrorContext? context = null)
        : base(KhefestSubsystem.Graphics, errorCode, message, innerException, context)
    {
    }

    public static KhefestGraphicsException DeviceLost(string details, Exception? inner = null)
    {
        var ctx = new KhefestErrorContext(KhefestSubsystem.Graphics, KhefestErrorCode.GraphicsDeviceLost)
            .With("Reason", details);
        return new KhefestGraphicsException(KhefestErrorCode.GraphicsDeviceLost, $"Graphics device was lost: {details}", inner, ctx);
    }

    public static KhefestGraphicsException ShaderCompilationFailed(string shaderName, string errorOutput)
    {
        var ctx = new KhefestErrorContext(KhefestSubsystem.Graphics, KhefestErrorCode.GraphicsShaderCompilationFailed)
            .With("Shader", shaderName)
            .With("CompilerOutput", errorOutput);
        return new KhefestGraphicsException(KhefestErrorCode.GraphicsShaderCompilationFailed, $"Failed to compile shader '{shaderName}': {errorOutput}", null, ctx);
    }
}

/// <summary>
/// Exception thrown during platform-specific operations (Win32 windowing, message loops, display management).
/// </summary>
public sealed class KhefestPlatformException : KhefestException
{
    public KhefestPlatformException(
        KhefestErrorCode errorCode,
        string message,
        int? nativeErrorCode = null,
        Exception? innerException = null,
        KhefestErrorContext? context = null)
        : base(
            KhefestSubsystem.Platform,
            errorCode,
            nativeErrorCode.HasValue ? $"{message} (Win32 error: 0x{nativeErrorCode.Value:X8} / {nativeErrorCode.Value})" : message,
            innerException,
            (context ?? new KhefestErrorContext(KhefestSubsystem.Platform, errorCode)).With("NativeErrorCode", nativeErrorCode))
    {
    }
}

/// <summary>
/// Exception thrown when a resource allocation, lifetime violation, or memory leak occurs.
/// </summary>
public sealed class KhefestResourceException : KhefestException
{
    public KhefestResourceException(
        KhefestErrorCode errorCode,
        string message,
        Exception? innerException = null,
        KhefestErrorContext? context = null)
        : base(KhefestSubsystem.Resource, errorCode, message, innerException, context)
    {
    }

    public static KhefestResourceException AlreadyDisposed(string resourceName, ulong resourceId)
    {
        var ctx = new KhefestErrorContext(KhefestSubsystem.Resource, KhefestErrorCode.ResourceAlreadyDisposed)
            .With("ResourceName", resourceName)
            .With("ResourceId", resourceId);
        return new KhefestResourceException(KhefestErrorCode.ResourceAlreadyDisposed, $"Resource '{resourceName}' (ID {resourceId}) has already been disposed.", null, ctx);
    }

    public static KhefestResourceException LeakDetected(string resourceName, ulong resourceId, string? allocationStackTrace)
    {
        var ctx = new KhefestErrorContext(KhefestSubsystem.Resource, KhefestErrorCode.ResourceLeakDetected)
            .With("ResourceName", resourceName)
            .With("ResourceId", resourceId)
            .With("AllocationStack", allocationStackTrace);
        return new KhefestResourceException(KhefestErrorCode.ResourceLeakDetected, $"Resource leak detected: '{resourceName}' (ID {resourceId}) was not disposed.", null, ctx);
    }
}

/// <summary>
/// Exception thrown during asset loading, parsing, or decoding.
/// </summary>
public sealed class KhefestAssetException : KhefestException
{
    public KhefestAssetException(
        KhefestErrorCode errorCode,
        string assetPath,
        string message,
        Exception? innerException = null)
        : base(
            KhefestSubsystem.Assets,
            errorCode,
            $"Asset '{assetPath}': {message}",
            innerException,
            new KhefestErrorContext(KhefestSubsystem.Assets, errorCode).With("AssetPath", assetPath))
    {
    }
}

/// <summary>
/// Exception thrown during plugin discovery, loading, dependency resolution, or execution.
/// </summary>
public sealed class KhefestPluginException : KhefestException
{
    public KhefestPluginException(
        KhefestErrorCode errorCode,
        string message,
        Exception? innerException = null,
        KhefestErrorContext? context = null)
        : base(KhefestSubsystem.Plugins, errorCode, message, innerException, context)
    {
    }
}
