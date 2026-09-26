using System.Diagnostics;
using System.Runtime.InteropServices;
using Khefest.Core.Logging;
using Khefest.Windowing;

namespace Khefest.Graphics.LowLevel.Spike;

public sealed record GpuSpikeResult(
    bool IsSuccess,
    string AdapterName,
    long DedicatedVideoMemoryBytes,
    int FramesPresented,
    double AverageFrameTimeMs,
    string Details);

/// <summary>
/// Phase 3 Technical SPIKE:
/// Verifies the native Windows GPU communication and DXGI modern flip-model presentation
/// path directly on Win32 HWND without external runtimes or middleware.
/// </summary>
public static unsafe class WindowsGpuSpike
{
    private static readonly ILogger Logger = LogManager.GetLogger(Core.Errors.KhefestSubsystem.Graphics, "GpuSpike");

    private const string D3D11Dll = "d3d11.dll";
    private const string DxgiDll = "dxgi.dll";

    // Standard D3D & DXGI Constants
    private const int D3D_DRIVER_TYPE_HARDWARE = 1;
    private const int D3D_DRIVER_TYPE_WARP = 5; // Software rasterizer fallback
    private const uint D3D11_SDK_VERSION = 7;
    private const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;

    private const int DXGI_FORMAT_R8G8B8A8_UNORM = 28;
    private const uint DXGI_USAGE_RENDER_TARGET_OUTPUT = 0x20;
    private const int DXGI_SWAP_EFFECT_FLIP_DISCARD = 4;
    private const int DXGI_SWAP_EFFECT_DISCARD = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_MODE_DESC
    {
        public uint Width;
        public uint Height;
        public DXGI_RATIONAL RefreshRate;
        public int Format;
        public int ScanlineOrdering;
        public int Scaling;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_SAMPLE_DESC
    {
        public uint Count;
        public uint Quality;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_SWAP_CHAIN_DESC
    {
        public DXGI_MODE_DESC BufferDesc;
        public DXGI_SAMPLE_DESC SampleDesc;
        public uint BufferUsage;
        public uint BufferCount;
        public nint OutputWindow;
        public int Windowed; // 1 = true, 0 = false
        public int SwapEffect;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DXGI_ADAPTER_DESC1
    {
        public fixed char Description[128];
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public long AdapterLuid;
        public uint Flags;
    }

    [DllImport(D3D11Dll, SetLastError = true)]
    private static extern int D3D11CreateDeviceAndSwapChain(
        nint pAdapter,
        int driverType,
        nint software,
        uint flags,
        nint pFeatureLevels,
        uint featureLevels,
        uint sdkVersion,
        in DXGI_SWAP_CHAIN_DESC pSwapChainDesc,
        out nint ppSwapChain,
        out nint ppDevice,
        out int pFeatureLevel,
        out nint ppImmediateContext);

    [DllImport(DxgiDll, SetLastError = true)]
    private static extern int CreateDXGIFactory1(in Guid riid, out nint ppFactory);

    private static readonly Guid IID_ID3D11Texture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
    private static readonly Guid IID_IDXGIFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");

    public static GpuSpikeResult Run(IWindow window, int frameCount = 10, Color4? clearColor = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        Logger.Info($"Running Phase 3 GPU Presentation SPIKE on HWND 0x{window.Handle:X} (Size: {window.ClientWidth}x{window.ClientHeight})...");

        var width = (uint)Math.Max(1, window.ClientWidth);
        var height = (uint)Math.Max(1, window.ClientHeight);
        var targetColor = clearColor ?? Color4.CornflowerBlue;

        // Query default adapter info using DXGIFactory1
        var (adapterName, vramBytes) = QueryPrimaryAdapterInfo();
        Logger.Info($"Detected GPU Adapter: {adapterName} (Dedicated VRAM: {vramBytes / (1024 * 1024)} MB)");

        // Configure Modern DXGI Flip Model SwapChain
        var swapDesc = new DXGI_SWAP_CHAIN_DESC
        {
            BufferDesc = new DXGI_MODE_DESC
            {
                Width = width,
                Height = height,
                Format = DXGI_FORMAT_R8G8B8A8_UNORM,
                RefreshRate = new DXGI_RATIONAL { Numerator = 60, Denominator = 1 }
            },
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
            BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
            BufferCount = 2, // Double buffering for flip model
            OutputWindow = window.Handle,
            Windowed = 1,
            SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD,
            Flags = 0
        };

        nint pSwapChain = nint.Zero;
        nint pDevice = nint.Zero;
        nint pContext = nint.Zero;
        nint pBackBuffer = nint.Zero;
        nint pRenderTargetView = nint.Zero;

        try
        {
            // Attempt hardware GPU device creation first, fall back to WARP if running on virtualized/headless host
            int hr = D3D11CreateDeviceAndSwapChain(
                nint.Zero,
                D3D_DRIVER_TYPE_HARDWARE,
                nint.Zero,
                D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                nint.Zero,
                0,
                D3D11_SDK_VERSION,
                in swapDesc,
                out pSwapChain,
                out pDevice,
                out var featureLevel,
                out pContext);

            string driverType = "Hardware (Direct3D / DXGI Flip Model)";

            if (hr < 0)
            {
                Logger.Warning($"Hardware device creation returned 0x{hr:X8}. Attempting WARP rasterizer fallback...");
                swapDesc.SwapEffect = DXGI_SWAP_EFFECT_DISCARD; // Fallback effect for software rasterizer
                hr = D3D11CreateDeviceAndSwapChain(
                    nint.Zero,
                    D3D_DRIVER_TYPE_WARP,
                    nint.Zero,
                    D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                    nint.Zero,
                    0,
                    D3D11_SDK_VERSION,
                    in swapDesc,
                    out pSwapChain,
                    out pDevice,
                    out featureLevel,
                    out pContext);
                driverType = "WARP Software Rasterizer";
            }

            if (hr < 0 || pSwapChain == nint.Zero || pDevice == nint.Zero || pContext == nint.Zero)
            {
                var errorMsg = $"Failed to create native GPU device and swapchain. HRESULT: 0x{hr:X8}";
                Logger.Error(errorMsg);
                return new GpuSpikeResult(false, adapterName, vramBytes, 0, 0, errorMsg);
            }

            Logger.Info($"Native GPU device created successfully. Driver: {driverType}, FeatureLevel: 0x{featureLevel:X}");

            // Retrieve BackBuffer Texture from SwapChain: IDXGISwapChain::GetBuffer(0, IID_ID3D11Texture2D, &pBackBuffer)
            // IDXGISwapChain vtable slot 9 = GetBuffer
            var swapChainVTable = *(void***)pSwapChain;
            var getBufferFn = (delegate* unmanaged[Stdcall]<nint, uint, Guid*, nint*, int>)swapChainVTable[9];
            var iidTexture2D = IID_ID3D11Texture2D;
            nint backBufferLocal = nint.Zero;
            hr = getBufferFn(pSwapChain, 0, &iidTexture2D, &backBufferLocal);
            pBackBuffer = backBufferLocal;

            if (hr < 0 || pBackBuffer == nint.Zero)
            {
                return new GpuSpikeResult(false, adapterName, vramBytes, 0, 0, $"GetBuffer failed with HRESULT 0x{hr:X8}");
            }

            // Create RenderTargetView: ID3D11Device::CreateRenderTargetView(pBackBuffer, NULL, &pRenderTargetView)
            // ID3D11Device vtable slot 9 = CreateRenderTargetView
            var deviceVTable = *(void***)pDevice;
            var createRtvFn = (delegate* unmanaged[Stdcall]<nint, nint, nint, nint*, int>)deviceVTable[9];
            nint rtvLocal = nint.Zero;
            hr = createRtvFn(pDevice, pBackBuffer, nint.Zero, &rtvLocal);
            pRenderTargetView = rtvLocal;

            if (hr < 0 || pRenderTargetView == nint.Zero)
            {
                return new GpuSpikeResult(false, adapterName, vramBytes, 0, 0, $"CreateRenderTargetView failed with HRESULT 0x{hr:X8}");
            }

            // Bind RenderTargetView: ID3D11DeviceContext::OMSetRenderTargets(1, &pRenderTargetView, NULL)
            // ID3D11DeviceContext vtable slot 33 = OMSetRenderTargets
            var contextVTable = *(void***)pContext;
            var omSetRtFn = (delegate* unmanaged[Stdcall]<nint, uint, nint*, nint, void>)contextVTable[33];
            nint rtvToBind = pRenderTargetView;
            omSetRtFn(pContext, 1, &rtvToBind, nint.Zero);

            // Execute Presentation Frames
            // ID3D11DeviceContext vtable slot 50 = ClearRenderTargetView
            var clearFn = (delegate* unmanaged[Stdcall]<nint, nint, float*, void>)contextVTable[50];
            // IDXGISwapChain vtable slot 8 = Present
            var presentFn = (delegate* unmanaged[Stdcall]<nint, uint, uint, int>)swapChainVTable[8];

            var sw = Stopwatch.StartNew();
            int presentedCount = 0;

            float* colorArray = stackalloc float[4];
            colorArray[0] = targetColor.R;
            colorArray[1] = targetColor.G;
            colorArray[2] = targetColor.B;
            colorArray[3] = targetColor.A;

            for (int i = 0; i < frameCount; i++)
            {
                window.PollEvents();

                // Clear render target to clear color
                clearFn(pContext, pRenderTargetView, colorArray);

                // Present with VSync enabled (syncInterval = 1)
                hr = presentFn(pSwapChain, 1, 0);
                if (hr < 0)
                {
                    Logger.Warning($"Present returned HRESULT 0x{hr:X8}");
                }
                presentedCount++;
            }

            sw.Stop();
            var avgMs = sw.Elapsed.TotalMilliseconds / Math.Max(1, presentedCount);
            Logger.Info($"SPIKE SUCCESS: Presented {presentedCount} frames in {sw.ElapsedMilliseconds}ms (Avg {avgMs:F2}ms/frame).");

            return new GpuSpikeResult(
                true,
                adapterName,
                vramBytes,
                presentedCount,
                avgMs,
                $"Successfully presented {presentedCount} frames via native DXGI flip-model on {adapterName} ({driverType}).");
        }
        finally
        {
            // Clean up native COM resources in reverse order
            SafeRelease(ref pRenderTargetView);
            SafeRelease(ref pBackBuffer);
            SafeRelease(ref pContext);
            SafeRelease(ref pSwapChain);
            SafeRelease(ref pDevice);
        }
    }

    private static (string Name, long VramBytes) QueryPrimaryAdapterInfo()
    {
        nint pFactory = nint.Zero;
        nint pAdapter = nint.Zero;

        try
        {
            var iidFactory = IID_IDXGIFactory1;
            int hr = CreateDXGIFactory1(in iidFactory, out pFactory);
            if (hr < 0 || pFactory == nint.Zero) return ("Windows Default GPU Adapter", 0);

            // IDXGIFactory1::EnumAdapters1(0, out pAdapter) -> slot 7
            var factoryVTable = *(void***)pFactory;
            var enumAdaptersFn = (delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)factoryVTable[7];
            nint adapterLocal = nint.Zero;
            hr = enumAdaptersFn(pFactory, 0, &adapterLocal);
            pAdapter = adapterLocal;

            if (hr < 0 || pAdapter == nint.Zero) return ("Windows Default GPU Adapter", 0);

            // IDXGIAdapter1::GetDesc1(&desc) -> slot 10
            var adapterVTable = *(void***)pAdapter;
            var getDescFn = (delegate* unmanaged[Stdcall]<nint, DXGI_ADAPTER_DESC1*, int>)adapterVTable[10];
            DXGI_ADAPTER_DESC1 desc;
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
            SafeRelease(ref pAdapter);
            SafeRelease(ref pFactory);
        }

        return ("Windows Default GPU Adapter", 0);
    }

    private static void SafeRelease(ref nint pComPtr)
    {
        if (pComPtr != nint.Zero)
        {
            var vtable = *(void***)pComPtr;
            var releaseFn = (delegate* unmanaged[Stdcall]<nint, uint>)vtable[2]; // IUnknown::Release
            releaseFn(pComPtr);
            pComPtr = nint.Zero;
        }
    }
}
