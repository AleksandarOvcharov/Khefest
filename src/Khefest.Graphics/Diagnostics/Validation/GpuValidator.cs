using Khefest.Core.Errors;
using Khefest.Core.Logging;
using Khefest.Graphics.LowLevel;

namespace Khefest.Graphics.Diagnostics.Validation;

/// <summary>
/// GPU graphics API validation layer for detecting contract violations, invalid state, and unbound resources.
/// </summary>
public sealed class GpuValidator
{
    private static readonly ILogger Logger = LogManager.GetLogger(KhefestSubsystem.Graphics, "GpuValidator");

    public ValidationMode Mode { get; set; }

    public GpuValidator(ValidationMode mode = ValidationMode.Strict)
    {
        Mode = mode;
    }

    /// <summary>
    /// Validates a non-indexed draw command.
    /// </summary>
    public void ValidateDraw(int vertexCount, int startVertexLocation, IGpuBuffer? vertexBuffer, IPipeline? pipeline)
    {
        if (Mode == ValidationMode.Disabled) return;

        if (vertexCount <= 0)
        {
            Fail("Draw call specifies non-positive vertexCount ({0}). Must draw at least 1 vertex.", vertexCount);
        }

        if (startVertexLocation < 0)
        {
            Fail("Draw call specifies negative startVertexLocation ({0}).", startVertexLocation);
        }

        if (vertexBuffer == null)
        {
            Fail("Draw call requires a bound vertex buffer, but none was bound.");
        }
        else if (vertexBuffer.IsDisposed)
        {
            Fail("Draw call references a disposed vertex buffer '{0}'.", vertexBuffer.Name);
        }

        if (pipeline == null)
        {
            Fail("Draw call requires an active GPU pipeline, but none was bound.");
        }
        else if (pipeline.IsDisposed)
        {
            Fail("Draw call references a disposed pipeline.");
        }
    }

    /// <summary>
    /// Validates an indexed draw command.
    /// </summary>
    public void ValidateDrawIndexed(
        int indexCount,
        int startIndexLocation,
        int baseVertexLocation,
        IGpuBuffer? indexBuffer,
        IGpuBuffer? vertexBuffer,
        IPipeline? pipeline)
    {
        if (Mode == ValidationMode.Disabled) return;

        if (indexCount <= 0)
        {
            Fail("Indexed draw call specifies non-positive indexCount ({0}). Must draw at least 1 index.", indexCount);
        }

        if (startIndexLocation < 0)
        {
            Fail("Indexed draw call specifies negative startIndexLocation ({0}).", startIndexLocation);
        }

        if (indexBuffer == null)
        {
            Fail("Indexed draw call requires a bound index buffer, but none was bound.");
        }
        else if (indexBuffer.IsDisposed)
        {
            Fail("Indexed draw call references a disposed index buffer '{0}'.", indexBuffer.Name);
        }

        if (vertexBuffer == null)
        {
            Fail("Indexed draw call requires a bound vertex buffer, but none was bound.");
        }
        else if (vertexBuffer.IsDisposed)
        {
            Fail("Indexed draw call references a disposed vertex buffer '{0}'.", vertexBuffer.Name);
        }

        if (pipeline == null)
        {
            Fail("Indexed draw call requires an active GPU pipeline, but none was bound.");
        }
        else if (pipeline.IsDisposed)
        {
            Fail("Indexed draw call references a disposed pipeline.");
        }
    }

    /// <summary>
    /// Validates buffer data upload parameters.
    /// </summary>
    public void ValidateBufferUpload(IGpuBuffer buffer, long offsetInBytes, long sizeInBytes)
    {
        if (Mode == ValidationMode.Disabled) return;

        if (buffer == null)
        {
            Fail("Cannot upload data to a null GPU buffer.");
            return;
        }

        if (buffer.IsDisposed)
        {
            Fail("Cannot upload data to disposed GPU buffer '{0}'.", buffer.Name);
        }

        if (offsetInBytes < 0)
        {
            Fail("Buffer upload offset cannot be negative ({0}).", offsetInBytes);
        }

        if (sizeInBytes <= 0)
        {
            Fail("Buffer upload size must be positive ({0}).", sizeInBytes);
        }

        if (offsetInBytes + sizeInBytes > buffer.SizeInBytes)
        {
            Fail(
                "Buffer upload range [{0}..{1}] exceeds buffer capacity of {2} bytes on '{3}'.",
                offsetInBytes,
                offsetInBytes + sizeInBytes,
                buffer.SizeInBytes,
                buffer.Name);
        }
    }

    /// <summary>
    /// Validates render target attachments and dimensions.
    /// </summary>
    public void ValidateRenderTargetAttachment(IGpuTexture? colorTarget, IGpuTexture? depthTarget)
    {
        if (Mode == ValidationMode.Disabled) return;

        if (colorTarget == null && depthTarget == null)
        {
            Fail("Render pass must bind at least one color target or depth target.");
            return;
        }

        if (colorTarget != null && colorTarget.IsDisposed)
        {
            Fail("Color render target '{0}' is disposed.", colorTarget.Name);
        }

        if (depthTarget != null && depthTarget.IsDisposed)
        {
            Fail("Depth render target '{0}' is disposed.", depthTarget.Name);
        }

        if (colorTarget != null && depthTarget != null)
        {
            if (colorTarget.Width != depthTarget.Width || colorTarget.Height != depthTarget.Height)
            {
                Fail(
                    "Mismatched render target dimensions: Color target '{0}' is {1}x{2}, while Depth target '{3}' is {4}x{5}.",
                    colorTarget.Name,
                    colorTarget.Width,
                    colorTarget.Height,
                    depthTarget.Name,
                    depthTarget.Width,
                    depthTarget.Height);
            }
        }
    }

    private void Fail(string format, params object?[] args)
    {
        string message = string.Format(format, args);
        if (Mode == ValidationMode.Strict)
        {
            throw new KhefestGraphicsException(KhefestErrorCode.InvalidOperation, message);
        }
        else if (Mode == ValidationMode.Warning)
        {
            Logger.Warning($"[Validation Warning] {message}");
        }
    }
}
