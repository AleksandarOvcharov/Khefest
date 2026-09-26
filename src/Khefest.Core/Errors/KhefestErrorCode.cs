namespace Khefest.Core.Errors;

/// <summary>
/// Categorized error codes across Khefest subsystems.
/// </summary>
public enum KhefestErrorCode
{
    // General / Core errors (0000-0999)
    None = 0,
    Unknown = 1,
    InvalidOperation = 2,
    ArgumentNull = 3,
    ArgumentOutOfRange = 4,
    NotSupported = 5,
    Timeout = 6,
    OutOfMemory = 7,
    ConfigurationInvalid = 8,
    InitializationFailed = 9,
    AlreadyInitialized = 10,
    NotInitialized = 11,

    // Platform / Windowing errors (1000-1999)
    PlatformWindowCreationFailed = 1000,
    PlatformWindowNotFound = 1001,
    PlatformWindowAlreadyExists = 1002,
    PlatformMessageLoopError = 1003,
    PlatformDisplayError = 1004,
    PlatformDpiError = 1005,
    PlatformNativeInteropError = 1006,

    // Graphics Low-Level & Driver errors (2000-2999)
    GraphicsDeviceCreationFailed = 2000,
    GraphicsDeviceLost = 2001,
    GraphicsDeviceNotSupported = 2002,
    GraphicsOutOfGpuMemory = 2003,
    GraphicsShaderCompilationFailed = 2004,
    GraphicsPipelineCreationFailed = 2005,
    GraphicsCommandSubmissionFailed = 2006,
    GraphicsSynchronizationFailed = 2007,
    GraphicsPresentationFailed = 2008,
    GraphicsSwapchainCreationFailed = 2009,
    GraphicsResourceBindingFailed = 2010,
    GraphicsFeatureNotSupported = 2011,

    // Resource Management errors (3000-3999)
    ResourceNotFound = 3000,
    ResourceAllocationFailed = 3001,
    ResourceAlreadyDisposed = 3002,
    ResourceCorrupted = 3003,
    ResourceInvalidHandle = 3004,
    ResourceLeakDetected = 3005,
    ResourceLimitExceeded = 3006,

    // Asset Loading errors (4000-4999)
    AssetNotFound = 4000,
    AssetLoadFailed = 4001,
    AssetFormatNotSupported = 4002,
    AssetCorrupted = 4003,

    // Audio errors (5000-5999)
    AudioDeviceInitFailed = 5000,
    AudioPlaybackFailed = 5001,

    // Input errors (6000-6999)
    InputDeviceNotFound = 6000,

    // UI & Plugin errors (7000-7999)
    UiLayoutError = 7000,
    PluginLoadFailed = 7001,
    PluginIncompatible = 7002
}
