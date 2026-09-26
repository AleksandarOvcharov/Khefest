using Khefest.Core.Resources;

namespace Khefest.Graphics.LowLevel.Windows;

/// <summary>
/// Win32 native implementation of <see cref="IGpuShader"/> backed by Direct3D 11.
/// </summary>
public sealed class WindowsGpuShader : ResourceBase, IGpuShader
{
    private nint _nativeShader;
    private readonly byte[] _bytecode;

    public ShaderStage Stage { get; }
    public string EntryPoint { get; }
    public nint NativeShader => _nativeShader;
    public ReadOnlySpan<byte> Bytecode => _bytecode;

    public WindowsGpuShader(
        string name,
        ShaderStage stage,
        string entryPoint,
        nint nativeShader,
        ReadOnlySpan<byte> bytecode,
        ResourceManager? manager = null)
        : base(name, ResourceType.Shader, bytecode.Length, manager?.Tracker.EnableLeakTracking ?? false)
    {
        Stage = stage;
        EntryPoint = entryPoint;
        _nativeShader = nativeShader;
        _bytecode = bytecode.ToArray();
        manager?.Register(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (_nativeShader != nint.Zero)
        {
            D3D11Native.SafeRelease(ref _nativeShader);
        }
    }
}
