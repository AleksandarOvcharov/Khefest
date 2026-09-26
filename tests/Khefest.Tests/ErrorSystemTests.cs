using Khefest.Core.Errors;
using Xunit;

namespace Khefest.Tests;

public class ErrorSystemTests
{
    [Fact]
    public void Exception_ContainsSubsystemAndErrorCode()
    {
        var context = new KhefestErrorContext(KhefestSubsystem.Graphics, KhefestErrorCode.GraphicsDeviceLost)
            .With("Adapter", "NVIDIA RTX 4090")
            .With("HRESULT", 0x887A0005);

        var ex = new KhefestGraphicsException(
            KhefestErrorCode.GraphicsDeviceLost,
            "Hardware device hung during draw dispatch",
            context: context);

        Assert.Equal(KhefestSubsystem.Graphics, ex.Subsystem);
        Assert.Equal(KhefestErrorCode.GraphicsDeviceLost, ex.ErrorCode);
        Assert.Contains("[Graphics]", ex.Message);
        Assert.Contains("GraphicsDeviceLost", ex.Message);
        Assert.Contains("NVIDIA RTX 4090", ex.Message);
        Assert.Equal("NVIDIA RTX 4090", ex.Context.Properties["Adapter"]);
    }

    [Fact]
    public void PlatformException_IncludesNativeErrorCode()
    {
        var ex = new KhefestPlatformException(
            KhefestErrorCode.PlatformWindowCreationFailed,
            "Failed to register Win32 window class",
            nativeErrorCode: 1400);

        Assert.Equal(KhefestSubsystem.Platform, ex.Subsystem);
        Assert.Equal(KhefestErrorCode.PlatformWindowCreationFailed, ex.ErrorCode);
        Assert.Contains("1400", ex.Message);
    }

    [Fact]
    public void KhefestResult_SuccessAndFailureBehavior()
    {
        var success = KhefestResult.Success();
        Assert.True(success.IsSuccess);
        Assert.False(success.IsFailure);
        Assert.Equal(KhefestErrorCode.None, success.ErrorCode);

        var failure = KhefestResult.Failure(KhefestSubsystem.Assets, KhefestErrorCode.AssetNotFound, "Missing texture file");
        Assert.False(failure.IsSuccess);
        Assert.True(failure.IsFailure);
        Assert.Equal(KhefestErrorCode.AssetNotFound, failure.ErrorCode);
        Assert.Equal("Missing texture file", failure.ErrorMessage);
    }

    [Fact]
    public void KhefestResultT_SuccessYieldsValue_FailureThrowsOnAccess()
    {
        var success = KhefestResult<int>.Success(42);
        Assert.True(success.IsSuccess);
        Assert.Equal(42, success.Value);
        Assert.True(success.TryGetValue(out var val));
        Assert.Equal(42, val);

        var failure = KhefestResult<int>.Failure(KhefestErrorCode.ResourceNotFound, "Resource ID missing");
        Assert.False(failure.IsSuccess);
        Assert.Throws<InvalidOperationException>(() => failure.Value);
        Assert.False(failure.TryGetValue(out _));
    }
}
