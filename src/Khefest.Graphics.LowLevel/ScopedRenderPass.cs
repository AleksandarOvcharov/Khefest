namespace Khefest.Graphics.LowLevel;

/// <summary>
/// Scoped handle for a render pass that guarantees <see cref="ICommandRecorder.EndPass"/>
/// is called when disposed, even if an unhandled exception occurs within the block.
/// </summary>
public struct ScopedRenderPass : IDisposable
{
    private readonly ICommandRecorder _recorder;
    private bool _isEnded;

    public ScopedRenderPass(ICommandRecorder recorder, in RenderPassDesc desc)
    {
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _recorder.BeginPass(desc);
        _isEnded = false;
    }

    public void Dispose()
    {
        if (!_isEnded)
        {
            _isEnded = true;
            _recorder.EndPass();
        }
    }
}
