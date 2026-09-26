using Khefest.Core.Resources;

namespace Khefest.Graphics.LowLevel.Windows;

/// <summary>
/// Win32 native implementation of <see cref="IPipeline"/> backed by Direct3D 11.
/// </summary>
public sealed class WindowsPipeline : ResourceBase, IPipeline
{
    private nint _inputLayout;
    private nint _rasterizerState;
    private nint _blendState;
    private nint _depthStencilState;

    public PipelineDesc Description { get; }
    public nint NativeInputLayout => _inputLayout;
    public nint NativeRasterizerState => _rasterizerState;
    public nint NativeBlendState => _blendState;
    public nint NativeDepthStencilState => _depthStencilState;

    public WindowsPipeline(
        string name,
        PipelineDesc desc,
        nint inputLayout,
        nint rasterizerState,
        nint blendState,
        nint depthStencilState,
        ResourceManager? manager = null)
        : base(name, ResourceType.Pipeline, 0, manager?.Tracker.EnableLeakTracking ?? false)
    {
        Description = desc;
        _inputLayout = inputLayout;
        _rasterizerState = rasterizerState;
        _blendState = blendState;
        _depthStencilState = depthStencilState;
        manager?.Register(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (_depthStencilState != nint.Zero)
        {
            D3D11Native.SafeRelease(ref _depthStencilState);
        }

        if (_blendState != nint.Zero)
        {
            D3D11Native.SafeRelease(ref _blendState);
        }

        if (_rasterizerState != nint.Zero)
        {
            D3D11Native.SafeRelease(ref _rasterizerState);
        }

        if (_inputLayout != nint.Zero)
        {
            D3D11Native.SafeRelease(ref _inputLayout);
        }
    }
}
