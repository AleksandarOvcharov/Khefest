using System.Runtime.InteropServices;
using System.Text;
using Khefest.Core.Errors;
using Khefest.Core.Logging;
using Khefest.Core.Resources;
using Khefest.Windowing;

namespace Khefest.Graphics.LowLevel.Windows;

/// <summary>
/// Win32 native implementation of <see cref="IGpuDevice"/> backed by Direct3D 11 and DXGI.
/// </summary>
public sealed unsafe class WindowsGpuDevice : IGpuDevice
{
    private static readonly ILogger Logger = LogManager.GetLogger(KhefestSubsystem.Graphics, "WindowsGpuDevice");

    private readonly ResourceManager? _resourceManager;
    private nint _device;
    private nint _context;
    private readonly void** _deviceVTable;
    private readonly void** _contextVTable;
    private readonly string _adapterName;
    private readonly long _vramBytes;
    private int _disposed;

    public string AdapterName => _adapterName;
    public long DedicatedVideoMemory => _vramBytes;
    public nint NativeDevice => _device;
    public nint NativeContext => _context;

    public WindowsGpuDevice(ResourceManager? resourceManager = null)
    {
        _resourceManager = resourceManager;

        var (name, vram) = QueryPrimaryAdapterInfo();
        _adapterName = name;
        _vramBytes = vram;

        int hr = D3D11Native.D3D11CreateDevice(
            nint.Zero,
            D3D11Native.D3D_DRIVER_TYPE_HARDWARE,
            nint.Zero,
            D3D11Native.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            nint.Zero,
            0,
            D3D11Native.D3D11_SDK_VERSION,
            out _device,
            out var featureLevel,
            out _context);

        if (hr < 0)
        {
            Logger.Warning($"Hardware D3D11 device creation failed (0x{hr:X8}). Falling back to WARP rasterizer...");
            hr = D3D11Native.D3D11CreateDevice(
                nint.Zero,
                D3D11Native.D3D_DRIVER_TYPE_WARP,
                nint.Zero,
                D3D11Native.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                nint.Zero,
                0,
                D3D11Native.D3D11_SDK_VERSION,
                out _device,
                out featureLevel,
                out _context);
        }

        if (hr < 0 || _device == nint.Zero || _context == nint.Zero)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.GraphicsDeviceCreationFailed,
                $"Failed to initialize native Windows GPU device (0x{hr:X8}).");
        }

        _deviceVTable = *(void***)_device;
        _contextVTable = *(void***)_context;

        Logger.Info($"GPU Device initialized: {_adapterName} (VRAM: {_vramBytes / (1024 * 1024)} MB, FeatureLevel: 0x{featureLevel:X})");
    }

    public IGpuBuffer CreateBuffer(string name, long sizeInBytes, BufferUsage usage)
    {
        ThrowIfDisposed();
        if (sizeInBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Buffer size must be greater than zero.");
        }

        // Direct3D constant buffers must be a multiple of 16 bytes
        var actualSize = (uint)sizeInBytes;
        if (usage.HasFlag(BufferUsage.Uniform))
        {
            actualSize = (actualSize + 15) & ~15u;
        }

        uint bindFlags = 0;
        if (usage.HasFlag(BufferUsage.Vertex)) bindFlags |= D3D11Native.D3D11_BIND_VERTEX_BUFFER;
        if (usage.HasFlag(BufferUsage.Index)) bindFlags |= D3D11Native.D3D11_BIND_INDEX_BUFFER;
        if (usage.HasFlag(BufferUsage.Uniform)) bindFlags |= D3D11Native.D3D11_BIND_CONSTANT_BUFFER;
        if (usage.HasFlag(BufferUsage.Storage)) bindFlags |= D3D11Native.D3D11_BIND_SHADER_RESOURCE;

        var desc = new D3D11Native.D3D11_BUFFER_DESC
        {
            ByteWidth = actualSize,
            Usage = D3D11Native.D3D11_USAGE_DEFAULT,
            BindFlags = bindFlags,
            CPUAccessFlags = 0,
            MiscFlags = 0,
            StructureByteStride = 0
        };

        // slot 3 = CreateBuffer
        var createBufferFn = (delegate* unmanaged[Stdcall]<nint, in D3D11Native.D3D11_BUFFER_DESC, void*, nint*, int>)_deviceVTable[3];
        nint pBuffer = nint.Zero;
        int hr = createBufferFn(_device, in desc, null, &pBuffer);

        if (hr < 0 || pBuffer == nint.Zero)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.ResourceAllocationFailed,
                $"Failed to create GPU buffer '{name}' of {actualSize} bytes (0x{hr:X8}).");
        }

        return new WindowsGpuBuffer(name, pBuffer, actualSize, usage, _context, _resourceManager);
    }

    public IGpuTexture CreateTexture(string name, int width, int height, GpuFormat format, TextureUsage usage)
    {
        ThrowIfDisposed();
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Texture dimensions must be positive.");
        }

        uint bindFlags = 0;
        if (usage.HasFlag(TextureUsage.RenderTarget)) bindFlags |= D3D11Native.D3D11_BIND_RENDER_TARGET;
        if (usage.HasFlag(TextureUsage.Sampled)) bindFlags |= D3D11Native.D3D11_BIND_SHADER_RESOURCE;
        if (usage.HasFlag(TextureUsage.DepthStencil)) bindFlags |= D3D11Native.D3D11_BIND_DEPTH_STENCIL;

        var desc = new D3D11Native.D3D11_TEXTURE2D_DESC
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = (int)format,
            SampleDescCount = 1,
            SampleDescQuality = 0,
            Usage = D3D11Native.D3D11_USAGE_DEFAULT,
            BindFlags = bindFlags,
            CPUAccessFlags = 0,
            MiscFlags = 0
        };

        // slot 5 = CreateTexture2D
        var createTexFn = (delegate* unmanaged[Stdcall]<nint, in D3D11Native.D3D11_TEXTURE2D_DESC, void*, nint*, int>)_deviceVTable[5];
        nint pTexture = nint.Zero;
        int hr = createTexFn(_device, in desc, null, &pTexture);

        if (hr < 0 || pTexture == nint.Zero)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.ResourceAllocationFailed,
                $"Failed to create GPU texture '{name}' ({width}x{height}) (0x{hr:X8}).");
        }

        nint pRtv = nint.Zero;
        if (usage.HasFlag(TextureUsage.RenderTarget))
        {
            // slot 9 = CreateRenderTargetView
            var createRtvFn = (delegate* unmanaged[Stdcall]<nint, nint, void*, nint*, int>)_deviceVTable[9];
            hr = createRtvFn(_device, pTexture, null, &pRtv);
            if (hr < 0)
            {
                D3D11Native.SafeRelease(ref pTexture);
                throw new KhefestGraphicsException(
                    KhefestErrorCode.ResourceAllocationFailed,
                    $"Failed to create RenderTargetView for texture '{name}' (0x{hr:X8}).");
            }
        }

        nint pSrv = nint.Zero;
        if (usage.HasFlag(TextureUsage.Sampled))
        {
            // slot 7 = CreateShaderResourceView
            var createSrvFn = (delegate* unmanaged[Stdcall]<nint, nint, void*, nint*, int>)_deviceVTable[7];
            hr = createSrvFn(_device, pTexture, null, &pSrv);
            if (hr < 0)
            {
                D3D11Native.SafeRelease(ref pRtv);
                D3D11Native.SafeRelease(ref pTexture);
                throw new KhefestGraphicsException(
                    KhefestErrorCode.ResourceAllocationFailed,
                    $"Failed to create ShaderResourceView for texture '{name}' (0x{hr:X8}).");
            }
        }

        nint pDsv = nint.Zero;
        if (usage.HasFlag(TextureUsage.DepthStencil))
        {
            // slot 10 = CreateDepthStencilView
            var createDsvFn = (delegate* unmanaged[Stdcall]<nint, nint, void*, nint*, int>)_deviceVTable[10];
            hr = createDsvFn(_device, pTexture, null, &pDsv);
            if (hr < 0)
            {
                D3D11Native.SafeRelease(ref pSrv);
                D3D11Native.SafeRelease(ref pRtv);
                D3D11Native.SafeRelease(ref pTexture);
                throw new KhefestGraphicsException(
                    KhefestErrorCode.ResourceAllocationFailed,
                    $"Failed to create DepthStencilView for texture '{name}' (0x{hr:X8}).");
            }
        }

        return new WindowsGpuTexture(name, pTexture, width, height, format, usage, pRtv, pSrv, _resourceManager, pDsv, _context);
    }

    public IGpuShader CreateShader(string name, ShaderStage stage, ReadOnlySpan<byte> bytecode, string entryPoint = "main")
    {
        ThrowIfDisposed();
        if (bytecode.IsEmpty)
        {
            throw new ArgumentException("Bytecode cannot be empty.", nameof(bytecode));
        }

        fixed (byte* pCode = bytecode)
        {
            nint pShader = nint.Zero;
            int hr;

            if (stage == ShaderStage.Vertex)
            {
                // slot 12 = CreateVertexShader
                var createVsFn = (delegate* unmanaged[Stdcall]<nint, void*, nuint, nint, nint*, int>)_deviceVTable[12];
                hr = createVsFn(_device, pCode, (nuint)bytecode.Length, nint.Zero, &pShader);
            }
            else if (stage == ShaderStage.Pixel)
            {
                // slot 15 = CreatePixelShader
                var createPsFn = (delegate* unmanaged[Stdcall]<nint, void*, nuint, nint, nint*, int>)_deviceVTable[15];
                hr = createPsFn(_device, pCode, (nuint)bytecode.Length, nint.Zero, &pShader);
            }
            else
            {
                throw new NotSupportedException($"Shader stage '{stage}' is not supported yet.");
            }

            if (hr < 0 || pShader == nint.Zero)
            {
                throw new KhefestGraphicsException(
                    KhefestErrorCode.ResourceAllocationFailed,
                    $"Failed to create GPU shader '{name}' ({stage}) from bytecode (0x{hr:X8}).");
            }

            return new WindowsGpuShader(name, stage, entryPoint, pShader, bytecode, _resourceManager);
        }
    }

    public IGpuShader CompileShader(string name, ShaderStage stage, string sourceCode, string entryPoint = "main")
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCode);

        var targetProfile = stage switch
        {
            ShaderStage.Vertex => "vs_5_0",
            ShaderStage.Pixel => "ps_5_0",
            ShaderStage.Compute => "cs_5_0",
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
        };

        var sourceBytes = Encoding.UTF8.GetBytes(sourceCode);
        nint pCode = nint.Zero;
        nint pErrorMsgs = nint.Zero;

        fixed (byte* pSrc = sourceBytes)
        {
            int hr = D3D11Native.D3DCompile(
                pSrc,
                (nuint)sourceBytes.Length,
                name,
                null,
                null,
                entryPoint,
                targetProfile,
                0,
                0,
                out pCode,
                out pErrorMsgs);

            if (hr < 0)
            {
                string errorDetail = "Unknown compilation error.";
                if (pErrorMsgs != nint.Zero)
                {
                    var blobVTable2 = *(void***)pErrorMsgs;
                    var getBufPtr = (delegate* unmanaged[Stdcall]<nint, byte*>)blobVTable2[3];
                    var getBufSize = (delegate* unmanaged[Stdcall]<nint, nuint>)blobVTable2[4];
                    var pMsg = getBufPtr(pErrorMsgs);
                    var size = getBufSize(pErrorMsgs);
                    if (pMsg != null && size > 0)
                    {
                        errorDetail = Encoding.UTF8.GetString(pMsg, (int)size);
                    }
                    D3D11Native.SafeRelease(ref pErrorMsgs);
                }

                throw new KhefestGraphicsException(
                    KhefestErrorCode.GraphicsShaderCompilationFailed,
                    $"Shader compilation failed for '{name}' ({stage}):\n{errorDetail}");
            }

            if (pErrorMsgs != nint.Zero)
            {
                D3D11Native.SafeRelease(ref pErrorMsgs);
            }

            var blobVTable = *(void***)pCode;
            var getBufPtr2 = (delegate* unmanaged[Stdcall]<nint, byte*>)blobVTable[3];
            var getBufSize2 = (delegate* unmanaged[Stdcall]<nint, nuint>)blobVTable[4];
            var pBytecode = getBufPtr2(pCode);
            var byteLength = (int)getBufSize2(pCode);

            var span = new ReadOnlySpan<byte>(pBytecode, byteLength);
            var shader = CreateShader(name, stage, span, entryPoint);

            D3D11Native.SafeRelease(ref pCode);
            return shader;
        }
    }

    public IPipeline CreatePipeline(string name, in PipelineDesc desc)
    {
        ThrowIfDisposed();

        if (desc.VertexShader is not WindowsGpuShader vs || vs.IsDisposed)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.GraphicsPipelineCreationFailed,
                "PipelineDesc VertexShader must be a valid, undisposed WindowsGpuShader.");
        }

        if (desc.PixelShader is not WindowsGpuShader ps || ps.IsDisposed)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.GraphicsPipelineCreationFailed,
                "PipelineDesc PixelShader must be a valid, undisposed WindowsGpuShader.");
        }

        if (desc.VertexLayout != null && (desc.VertexLayout.Elements == null || desc.VertexLayout.Elements.Count == 0))
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.GraphicsPipelineCreationFailed,
                "PipelineDesc VertexLayout, when specified, must contain at least one VertexElement.");
        }

        nint pInputLayout = nint.Zero;
        if (desc.VertexLayout != null && desc.VertexLayout.Elements.Count > 0)
        {
            // 1. Create Input Layout
            var elements = desc.VertexLayout.Elements;
            var nativeElements = stackalloc D3D11Native.D3D11_INPUT_ELEMENT_DESC[elements.Count];
            var stringPointers = stackalloc nint[elements.Count];

            for (int i = 0; i < elements.Count; i++)
            {
                var el = elements[i];
                stringPointers[i] = Marshal.StringToHGlobalAnsi(el.SemanticName);
                nativeElements[i] = new D3D11Native.D3D11_INPUT_ELEMENT_DESC
                {
                    SemanticName = stringPointers[i],
                    SemanticIndex = (uint)el.SemanticIndex,
                    Format = (int)el.Format,
                    InputSlot = 0,
                    AlignedByteOffset = (uint)el.Offset,
                    InputSlotClass = 0, // PER_VERTEX
                    InstanceDataStepRate = 0
                };
            }

            try
            {
                fixed (byte* pVsCode = vs.Bytecode)
                {
                    // slot 11 = CreateInputLayout
                    var createLayoutFn = (delegate* unmanaged[Stdcall]<nint, D3D11Native.D3D11_INPUT_ELEMENT_DESC*, uint, void*, nuint, nint*, int>)_deviceVTable[11];
                    int hr = createLayoutFn(_device, nativeElements, (uint)elements.Count, pVsCode, (nuint)vs.Bytecode.Length, &pInputLayout);
                    if (hr < 0 || pInputLayout == nint.Zero)
                    {
                        throw new KhefestGraphicsException(
                            KhefestErrorCode.GraphicsPipelineCreationFailed,
                            $"Failed to create InputLayout for pipeline '{name}' (0x{hr:X8}).");
                    }
                }
            }
            finally
            {
                for (int i = 0; i < elements.Count; i++)
                {
                    if (stringPointers[i] != nint.Zero)
                    {
                        Marshal.FreeHGlobal(stringPointers[i]);
                    }
                }
            }
        }

        // 2. Create Rasterizer State: slot 22
        var rastDesc = new D3D11Native.D3D11_RASTERIZER_DESC
        {
            FillMode = desc.FillMode == FillMode.Wireframe ? D3D11Native.D3D11_FILL_WIREFRAME : D3D11Native.D3D11_FILL_SOLID,
            CullMode = desc.CullMode switch
            {
                CullMode.None => D3D11Native.D3D11_CULL_NONE,
                CullMode.Front => D3D11Native.D3D11_CULL_FRONT,
                _ => D3D11Native.D3D11_CULL_BACK
            },
            FrontCounterClockwise = 0,
            DepthBias = 0,
            DepthBiasClamp = 0,
            SlopeScaledDepthBias = 0,
            DepthClipEnable = 1,
            ScissorEnable = 0,
            MultisampleEnable = 0,
            AntialiasedLineEnable = 0
        };

        var createRastFn = (delegate* unmanaged[Stdcall]<nint, in D3D11Native.D3D11_RASTERIZER_DESC, nint*, int>)_deviceVTable[22];
        nint pRasterizer = nint.Zero;
        createRastFn(_device, in rastDesc, &pRasterizer);

        // 3. Create Blend State: slot 20
        var blendDesc = new D3D11Native.D3D11_BLEND_DESC();
        blendDesc.RenderTarget0.RenderTargetWriteMask = D3D11Native.D3D11_COLOR_WRITE_ENABLE_ALL;

        if (desc.BlendMode == BlendMode.AlphaBlend)
        {
            blendDesc.RenderTarget0.BlendEnable = 1;
            blendDesc.RenderTarget0.SrcBlend = D3D11Native.D3D11_BLEND_SRC_ALPHA;
            blendDesc.RenderTarget0.DestBlend = D3D11Native.D3D11_BLEND_INV_SRC_ALPHA;
            blendDesc.RenderTarget0.BlendOp = D3D11Native.D3D11_BLEND_OP_ADD;
            blendDesc.RenderTarget0.SrcBlendAlpha = D3D11Native.D3D11_BLEND_ONE;
            blendDesc.RenderTarget0.DestBlendAlpha = D3D11Native.D3D11_BLEND_ZERO;
            blendDesc.RenderTarget0.BlendOpAlpha = D3D11Native.D3D11_BLEND_OP_ADD;
        }

        var createBlendFn = (delegate* unmanaged[Stdcall]<nint, in D3D11Native.D3D11_BLEND_DESC, nint*, int>)_deviceVTable[20];
        nint pBlend = nint.Zero;
        createBlendFn(_device, in blendDesc, &pBlend);

        // 4. Create Depth Stencil State: slot 21
        var dsDesc = new D3D11Native.D3D11_DEPTH_STENCIL_DESC
        {
            DepthEnable = desc.DepthTestEnabled ? 1 : 0,
            DepthWriteMask = desc.DepthWriteEnabled ? D3D11Native.D3D11_DEPTH_WRITE_MASK_ALL : D3D11Native.D3D11_DEPTH_WRITE_MASK_ZERO,
            DepthFunc = desc.DepthFunction switch
            {
                CompareFunction.Less => D3D11Native.D3D11_COMPARISON_LESS,
                CompareFunction.LessEqual => D3D11Native.D3D11_COMPARISON_LESS_EQUAL,
                CompareFunction.Equal => D3D11Native.D3D11_COMPARISON_EQUAL,
                CompareFunction.Greater => D3D11Native.D3D11_COMPARISON_GREATER,
                CompareFunction.GreaterEqual => D3D11Native.D3D11_COMPARISON_GREATER_EQUAL,
                CompareFunction.NotEqual => D3D11Native.D3D11_COMPARISON_NOT_EQUAL,
                CompareFunction.Always => D3D11Native.D3D11_COMPARISON_ALWAYS,
                _ => D3D11Native.D3D11_COMPARISON_LESS
            }
        };

        var createDsFn = (delegate* unmanaged[Stdcall]<nint, in D3D11Native.D3D11_DEPTH_STENCIL_DESC, nint*, int>)_deviceVTable[21];
        nint pDepthStencil = nint.Zero;
        createDsFn(_device, in dsDesc, &pDepthStencil);

        return new WindowsPipeline(name, desc, pInputLayout, pRasterizer, pBlend, pDepthStencil, _resourceManager);
    }

    public ISwapChain CreateSwapChain(IWindow window, int width, int height, bool vsync = true)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(window);

        var swapDesc = new D3D11Native.DXGI_SWAP_CHAIN_DESC
        {
            BufferDesc = new D3D11Native.DXGI_MODE_DESC
            {
                Width = (uint)Math.Max(1, width),
                Height = (uint)Math.Max(1, height),
                Format = D3D11Native.DXGI_FORMAT_R8G8B8A8_UNORM,
                RefreshRate = new D3D11Native.DXGI_RATIONAL { Numerator = 60, Denominator = 1 }
            },
            SampleDesc = new D3D11Native.DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
            BufferUsage = D3D11Native.DXGI_USAGE_RENDER_TARGET_OUTPUT,
            BufferCount = 2,
            OutputWindow = window.Handle,
            Windowed = 1,
            SwapEffect = D3D11Native.DXGI_SWAP_EFFECT_FLIP_DISCARD,
            Flags = 0
        };

        // Query IDXGIFactory from device to create swapchain for HWND
        var iidFactory = D3D11Native.IID_IDXGIFactory1;
        int hr = D3D11Native.CreateDXGIFactory1(in iidFactory, out var pFactory);
        if (hr < 0 || pFactory == nint.Zero)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.GraphicsSwapchainCreationFailed,
                $"Failed to create DXGIFactory (0x{hr:X8}).");
        }

        // IDXGIFactory::CreateSwapChain -> slot 10
        var factoryVTable = *(void***)pFactory;
        var createScFn = (delegate* unmanaged[Stdcall]<nint, nint, in D3D11Native.DXGI_SWAP_CHAIN_DESC, nint*, int>)factoryVTable[10];
        nint pSwapChain = nint.Zero;
        hr = createScFn(pFactory, _device, in swapDesc, &pSwapChain);
        D3D11Native.SafeRelease(ref pFactory);

        if (hr < 0 || pSwapChain == nint.Zero)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.GraphicsSwapchainCreationFailed,
                $"Failed to create DXGI SwapChain for HWND 0x{window.Handle:X} (0x{hr:X8}).");
        }

        return new WindowsSwapChain(_device, pSwapChain, width, height, _resourceManager, _context);
    }

    public ICommandRecorder CreateCommandRecorder()
    {
        ThrowIfDisposed();
        return new WindowsCommandRecorder(_context);
    }

    public void Submit(ICommandRecorder recorder)
    {
        ThrowIfDisposed();
        // With immediate context recording, commands are streamed directly to the GPU command queue.
    }

    public void WaitForGpu()
    {
        ThrowIfDisposed();
        // slot 111 = Flush
        var flushFn = (delegate* unmanaged[Stdcall]<nint, void>)_contextVTable[111];
        flushFn(_context);
    }

    private static (string Name, long VramBytes) QueryPrimaryAdapterInfo()
    {
        nint pFactory = nint.Zero;
        nint pAdapter = nint.Zero;

        try
        {
            var iidFactory = D3D11Native.IID_IDXGIFactory1;
            int hr = D3D11Native.CreateDXGIFactory1(in iidFactory, out pFactory);
            if (hr < 0 || pFactory == nint.Zero) return ("Default GPU Adapter", 0);

            var factoryVTable = *(void***)pFactory;
            var enumAdaptersFn = (delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)factoryVTable[7];
            nint adapterLocal = nint.Zero;
            hr = enumAdaptersFn(pFactory, 0, &adapterLocal);
            pAdapter = adapterLocal;

            if (hr < 0 || pAdapter == nint.Zero) return ("Default GPU Adapter", 0);

            var adapterVTable = *(void***)pAdapter;
            var getDescFn = (delegate* unmanaged[Stdcall]<nint, D3D11Native.DXGI_ADAPTER_DESC1*, int>)adapterVTable[10];
            D3D11Native.DXGI_ADAPTER_DESC1 desc;
            hr = getDescFn(pAdapter, &desc);

            if (hr >= 0)
            {
                var name = new string(desc.Description);
                return (name, (long)desc.DedicatedVideoMemory);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("Failed to query primary adapter info", ex);
        }
        finally
        {
            D3D11Native.SafeRelease(ref pAdapter);
            D3D11Native.SafeRelease(ref pFactory);
        }

        return ("Default GPU Adapter", 0);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            D3D11Native.SafeRelease(ref _context);
            D3D11Native.SafeRelease(ref _device);
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new KhefestGraphicsException(
                KhefestErrorCode.InvalidOperation,
                "GpuDevice has been disposed.");
        }
    }
}
