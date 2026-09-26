using Khefest.Core.Errors;

namespace Khefest.Graphics.LowLevel.Windows;

/// <summary>
/// Win32 native implementation of <see cref="ICommandRecorder"/> backed by Direct3D 11.
/// </summary>
public sealed unsafe class WindowsCommandRecorder : ICommandRecorder
{
    private readonly nint _context;
    private readonly void** _contextVTable;
    private WindowsPipeline? _currentPipeline;
    private bool _isInPass;
    private int _disposed;

    public bool IsInPass => _isInPass;

    public WindowsCommandRecorder(nint context)
    {
        _context = context;
        _contextVTable = *(void***)context;
    }

    public void BeginPass(in RenderPassDesc pass)
    {
        ThrowIfDisposed();

        if (_isInPass)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.InvalidOperation,
                "Cannot call BeginPass while a render pass is already active. Call EndPass first.");
        }

        if (pass.ColorTarget == null && (pass.ColorTargets == null || pass.ColorTargets.Count == 0) && pass.DepthTarget == null)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.GraphicsResourceBindingFailed,
                "RenderPassDesc must specify at least a ColorTarget, ColorTargets (MRT), or DepthTarget.");
        }

        nint dsv = nint.Zero;
        if (pass.DepthTarget != null)
        {
            var depthTex = ResolveNativeTexture(pass.DepthTarget, "DepthTarget");
            dsv = depthTex.NativeDepthStencilView;
        }

        var omSetRt = (delegate* unmanaged[Stdcall]<nint, uint, nint*, nint, void>)_contextVTable[33];
        int vpWidth = 1;
        int vpHeight = 1;

        // 1. Multiple Render Targets (MRT)
        if (pass.ColorTargets != null && pass.ColorTargets.Count > 0)
        {
            int rtCount = pass.ColorTargets.Count;
            if (rtCount > 8)
            {
                throw new KhefestGraphicsException(
                    KhefestErrorCode.GraphicsResourceBindingFailed,
                    $"Direct3D 11 supports a maximum of 8 simultaneous render targets, but {rtCount} were provided.");
            }

            var rtvs = stackalloc nint[rtCount];
            for (int i = 0; i < rtCount; i++)
            {
                var wt = ResolveNativeTexture(pass.ColorTargets[i], $"ColorTargets[{i}]");
                rtvs[i] = wt.NativeRenderTargetView;
                if (i == 0)
                {
                    vpWidth = wt.Width;
                    vpHeight = wt.Height;
                }
            }

            omSetRt(_context, (uint)rtCount, rtvs, dsv);

            if (pass.ClearColorTarget)
            {
                float* colorArray = stackalloc float[4];
                colorArray[0] = pass.ClearColor.R;
                colorArray[1] = pass.ClearColor.G;
                colorArray[2] = pass.ClearColor.B;
                colorArray[3] = pass.ClearColor.A;

                var clearRt = (delegate* unmanaged[Stdcall]<nint, nint, float*, void>)_contextVTable[50];
                for (int i = 0; i < rtCount; i++)
                {
                    if (rtvs[i] != nint.Zero)
                    {
                        clearRt(_context, rtvs[i], colorArray);
                    }
                }
            }
        }
        // 2. Single Render Target
        else if (pass.ColorTarget != null)
        {
            var colorTex = ResolveNativeTexture(pass.ColorTarget, "ColorTarget");
            var rtv = colorTex.NativeRenderTargetView;
            if (rtv == nint.Zero)
            {
                throw new KhefestGraphicsException(
                    KhefestErrorCode.GraphicsResourceBindingFailed,
                    $"ColorTarget '{colorTex.Name}' does not have a RenderTargetView.");
            }

            vpWidth = colorTex.Width;
            vpHeight = colorTex.Height;

            omSetRt(_context, 1, &rtv, dsv);

            if (pass.ClearColorTarget)
            {
                float* colorArray = stackalloc float[4];
                colorArray[0] = pass.ClearColor.R;
                colorArray[1] = pass.ClearColor.G;
                colorArray[2] = pass.ClearColor.B;
                colorArray[3] = pass.ClearColor.A;

                var clearRt = (delegate* unmanaged[Stdcall]<nint, nint, float*, void>)_contextVTable[50];
                clearRt(_context, rtv, colorArray);
            }
        }
        // 3. Depth-Only Pass (e.g. shadow mapping or depth pre-pass)
        else
        {
            if (dsv == nint.Zero)
            {
                throw new KhefestGraphicsException(
                    KhefestErrorCode.GraphicsResourceBindingFailed,
                    "Depth-only pass requires a valid DepthTarget with a DepthStencilView.");
            }

            if (pass.DepthTarget != null)
            {
                var dt = ResolveNativeTexture(pass.DepthTarget, "DepthTarget");
                vpWidth = dt.Width;
                vpHeight = dt.Height;
            }

            omSetRt(_context, 0, null, dsv);
        }

        // Clear Depth Target if requested: slot 53 = ClearDepthStencilView
        if (pass.ClearDepthTarget && dsv != nint.Zero)
        {
            const uint D3D11_CLEAR_DEPTH = 0x1;
            const uint D3D11_CLEAR_STENCIL = 0x2;
            var clearDsv = (delegate* unmanaged[Stdcall]<nint, nint, uint, float, byte, void>)_contextVTable[53];
            clearDsv(_context, dsv, D3D11_CLEAR_DEPTH | D3D11_CLEAR_STENCIL, pass.ClearDepth, 0);
        }

        // Set default viewport matching target dimensions: slot 44 = RSSetViewports
        var vp = new D3D11Native.D3D11_VIEWPORT
        {
            TopLeftX = 0,
            TopLeftY = 0,
            Width = vpWidth,
            Height = vpHeight,
            MinDepth = 0.0f,
            MaxDepth = 1.0f
        };
        var rsSetVp = (delegate* unmanaged[Stdcall]<nint, uint, D3D11Native.D3D11_VIEWPORT*, void>)_contextVTable[44];
        rsSetVp(_context, 1, &vp);

        _isInPass = true;
    }

    public ScopedRenderPass BeginScopedPass(in RenderPassDesc pass)
    {
        return new ScopedRenderPass(this, pass);
    }

    public void SetPipeline(IPipeline pipeline)
    {
        ThrowIfDisposed();
        ThrowIfNotInPass();

        if (pipeline is not WindowsPipeline winPipeline || winPipeline.IsDisposed)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.GraphicsResourceBindingFailed,
                "Pipeline must be a valid, undisposed WindowsPipeline.");
        }

        _currentPipeline = winPipeline;

        // 1. Input Layout: slot 17 = IASetInputLayout
        var setInputLayout = (delegate* unmanaged[Stdcall]<nint, nint, void>)_contextVTable[17];
        setInputLayout(_context, winPipeline.NativeInputLayout);

        // 2. Vertex Shader: slot 11 = VSSetShader
        if (winPipeline.Description.VertexShader is WindowsGpuShader vs)
        {
            var vsSetShader = (delegate* unmanaged[Stdcall]<nint, nint, nint*, uint, void>)_contextVTable[11];
            vsSetShader(_context, vs.NativeShader, null, 0);
        }

        // 3. Pixel Shader: slot 9 = PSSetShader
        if (winPipeline.Description.PixelShader is WindowsGpuShader ps)
        {
            var psSetShader = (delegate* unmanaged[Stdcall]<nint, nint, nint*, uint, void>)_contextVTable[9];
            psSetShader(_context, ps.NativeShader, null, 0);
        }

        // 4. Rasterizer State: slot 43 = RSSetState
        var rsSetState = (delegate* unmanaged[Stdcall]<nint, nint, void>)_contextVTable[43];
        rsSetState(_context, winPipeline.NativeRasterizerState);

        // 5. Blend State: slot 35 = OMSetBlendState
        var omSetBlend = (delegate* unmanaged[Stdcall]<nint, nint, float*, uint, void>)_contextVTable[35];
        omSetBlend(_context, winPipeline.NativeBlendState, null, 0xFFFFFFFF);

        // 6. Depth Stencil State: slot 36 = OMSetDepthStencilState
        var omSetDepth = (delegate* unmanaged[Stdcall]<nint, nint, uint, void>)_contextVTable[36];
        omSetDepth(_context, winPipeline.NativeDepthStencilState, 0);

        // 7. Primitive Topology: slot 24 = IASetPrimitiveTopology
        var d3dTopology = winPipeline.Description.Topology switch
        {
            PrimitiveTopology.TriangleList => D3D11Native.D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST,
            PrimitiveTopology.TriangleStrip => D3D11Native.D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP,
            PrimitiveTopology.LineList => D3D11Native.D3D11_PRIMITIVE_TOPOLOGY_LINELIST,
            PrimitiveTopology.LineStrip => D3D11Native.D3D11_PRIMITIVE_TOPOLOGY_LINESTRIP,
            PrimitiveTopology.PointList => D3D11Native.D3D11_PRIMITIVE_TOPOLOGY_POINTLIST,
            _ => D3D11Native.D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST
        };
        var iaSetTopology = (delegate* unmanaged[Stdcall]<nint, int, void>)_contextVTable[24];
        iaSetTopology(_context, d3dTopology);
    }

    public void SetVertexBuffer(int slot, IGpuBuffer buffer)
    {
        ThrowIfDisposed();
        ThrowIfNotInPass();

        if (buffer is not WindowsGpuBuffer winBuffer || winBuffer.IsDisposed)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.GraphicsResourceBindingFailed,
                "VertexBuffer must be a valid, undisposed WindowsGpuBuffer.");
        }

        var pBuf = winBuffer.NativeBuffer;
        uint stride = (uint)(_currentPipeline?.Description.VertexLayout?.Stride ?? 0);
        uint offset = 0;

        // slot 18 = IASetVertexBuffers
        var iaSetVb = (delegate* unmanaged[Stdcall]<nint, uint, uint, nint*, uint*, uint*, void>)_contextVTable[18];
        iaSetVb(_context, (uint)slot, 1, &pBuf, &stride, &offset);
    }

    public void SetIndexBuffer(IGpuBuffer buffer, IndexFormat format)
    {
        ThrowIfDisposed();
        ThrowIfNotInPass();

        if (buffer is not WindowsGpuBuffer winBuffer || winBuffer.IsDisposed)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.GraphicsResourceBindingFailed,
                "IndexBuffer must be a valid, undisposed WindowsGpuBuffer.");
        }

        var dxgiFormat = format == IndexFormat.UInt16 ? 57 /* DXGI_FORMAT_R16_UINT */ : 42 /* DXGI_FORMAT_R32_UINT */;
        // slot 19 = IASetIndexBuffer
        var iaSetIb = (delegate* unmanaged[Stdcall]<nint, nint, int, uint, void>)_contextVTable[19];
        iaSetIb(_context, winBuffer.NativeBuffer, dxgiFormat, 0);
    }

    public void SetUniformBuffer(int slot, IGpuBuffer buffer)
    {
        ThrowIfDisposed();
        ThrowIfNotInPass();

        if (buffer is not WindowsGpuBuffer winBuffer || winBuffer.IsDisposed)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.GraphicsResourceBindingFailed,
                "UniformBuffer must be a valid, undisposed WindowsGpuBuffer.");
        }

        var pBuf = winBuffer.NativeBuffer;
        // slot 7 = VSSetConstantBuffers
        var vsSetCb = (delegate* unmanaged[Stdcall]<nint, uint, uint, nint*, void>)_contextVTable[7];
        vsSetCb(_context, (uint)slot, 1, &pBuf);

        // slot 16 = PSSetConstantBuffers
        var psSetCb = (delegate* unmanaged[Stdcall]<nint, uint, uint, nint*, void>)_contextVTable[16];
        psSetCb(_context, (uint)slot, 1, &pBuf);
    }

    public void SetTexture(int slot, IGpuTexture texture)
    {
        ThrowIfDisposed();
        ThrowIfNotInPass();

        var winTex = ResolveNativeTexture(texture, "Texture");
        var srv = winTex.NativeShaderResourceView;
        // slot 8 = PSSetShaderResources
        var psSetSrv = (delegate* unmanaged[Stdcall]<nint, uint, uint, nint*, void>)_contextVTable[8];
        psSetSrv(_context, (uint)slot, 1, &srv);
    }

    public void SetViewport(in Viewport viewport)
    {
        ThrowIfDisposed();
        ThrowIfNotInPass();

        var vp = new D3D11Native.D3D11_VIEWPORT
        {
            TopLeftX = viewport.X,
            TopLeftY = viewport.Y,
            Width = viewport.Width,
            Height = viewport.Height,
            MinDepth = viewport.MinDepth,
            MaxDepth = viewport.MaxDepth
        };
        // slot 44 = RSSetViewports
        var rsSetVp = (delegate* unmanaged[Stdcall]<nint, uint, D3D11Native.D3D11_VIEWPORT*, void>)_contextVTable[44];
        rsSetVp(_context, 1, &vp);
    }

    public void SetScissor(in ScissorRect scissor)
    {
        ThrowIfDisposed();
        ThrowIfNotInPass();

        var rect = new D3D11Native.RECT
        {
            Left = scissor.X,
            Top = scissor.Y,
            Right = scissor.X + scissor.Width,
            Bottom = scissor.Y + scissor.Height
        };
        // slot 45 = RSSetScissorRects
        var rsSetScissor = (delegate* unmanaged[Stdcall]<nint, uint, D3D11Native.RECT*, void>)_contextVTable[45];
        rsSetScissor(_context, 1, &rect);
    }

    public void Draw(int vertexCount, int firstVertex = 0)
    {
        ThrowIfDisposed();
        ThrowIfNotInPass();

        // slot 13 = Draw
        var draw = (delegate* unmanaged[Stdcall]<nint, uint, uint, void>)_contextVTable[13];
        draw(_context, (uint)vertexCount, (uint)firstVertex);
    }

    public void DrawIndexed(int indexCount, int firstIndex = 0, int vertexOffset = 0)
    {
        ThrowIfDisposed();
        ThrowIfNotInPass();

        // slot 12 = DrawIndexed
        var drawIndexed = (delegate* unmanaged[Stdcall]<nint, uint, uint, int, void>)_contextVTable[12];
        drawIndexed(_context, (uint)indexCount, (uint)firstIndex, vertexOffset);
    }

    public void EndPass()
    {
        ThrowIfDisposed();
        if (!_isInPass)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.InvalidOperation,
                "Cannot call EndPass without an active render pass. Call BeginPass first.");
        }

        // Unbind render targets: slot 33 = OMSetRenderTargets
        nint nullRtv = nint.Zero;
        var omSetRt = (delegate* unmanaged[Stdcall]<nint, uint, nint*, nint, void>)_contextVTable[33];
        omSetRt(_context, 0, null, nint.Zero);

        _isInPass = false;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            if (_isInPass)
            {
                try { EndPass(); } catch { }
            }
        }
    }

    private static WindowsGpuTexture ResolveNativeTexture(IGpuTexture texture, string targetName)
    {
        ArgumentNullException.ThrowIfNull(texture, targetName);
        var resolved = texture.CanonicalTexture;
        if (resolved is not WindowsGpuTexture winTarget || winTarget.IsDisposed)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.GraphicsResourceBindingFailed,
                $"{targetName} must be a valid, undisposed WindowsGpuTexture.");
        }
        return winTarget;
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.InvalidOperation,
                "CommandRecorder has been disposed.");
        }
    }

    private void ThrowIfNotInPass()
    {
        if (!_isInPass)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.InvalidOperation,
                "Render commands must be executed within an active render pass (between BeginPass and EndPass).");
        }
    }
}
