using System.Runtime.InteropServices;

namespace Khefest.Graphics.LowLevel.Windows;

internal static unsafe class D3D11Native
{
    public const string D3D11Dll = "d3d11.dll";
    public const string DxgiDll = "dxgi.dll";
    public const string D3DCompilerDll = "d3dcompiler_47.dll";

    public const uint D3D11_SDK_VERSION = 7;
    public const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;

    public const int D3D_DRIVER_TYPE_HARDWARE = 1;
    public const int D3D_DRIVER_TYPE_WARP = 5;

    public const int D3D11_USAGE_DEFAULT = 0;
    public const int D3D11_USAGE_IMMUTABLE = 1;
    public const int D3D11_USAGE_DYNAMIC = 2;
    public const int D3D11_USAGE_STAGING = 3;

    public const uint D3D11_BIND_VERTEX_BUFFER = 0x1;
    public const uint D3D11_BIND_INDEX_BUFFER = 0x2;
    public const uint D3D11_BIND_CONSTANT_BUFFER = 0x4;
    public const uint D3D11_BIND_SHADER_RESOURCE = 0x8;
    public const uint D3D11_BIND_RENDER_TARGET = 0x20;
    public const uint D3D11_BIND_DEPTH_STENCIL = 0x40;

    public const uint D3D11_CPU_ACCESS_WRITE = 0x10000;
    public const uint D3D11_CPU_ACCESS_READ = 0x20000;

    public const int D3D11_CULL_NONE = 1;
    public const int D3D11_CULL_FRONT = 2;
    public const int D3D11_CULL_BACK = 3;

    public const int D3D11_FILL_WIREFRAME = 2;
    public const int D3D11_FILL_SOLID = 3;

    public const int D3D11_COMPARISON_NEVER = 1;
    public const int D3D11_COMPARISON_LESS = 2;
    public const int D3D11_COMPARISON_EQUAL = 3;
    public const int D3D11_COMPARISON_LESS_EQUAL = 4;
    public const int D3D11_COMPARISON_GREATER = 5;
    public const int D3D11_COMPARISON_NOT_EQUAL = 6;
    public const int D3D11_COMPARISON_GREATER_EQUAL = 7;
    public const int D3D11_COMPARISON_ALWAYS = 8;

    public const int D3D11_DEPTH_WRITE_MASK_ZERO = 0;
    public const int D3D11_DEPTH_WRITE_MASK_ALL = 1;

    public const int D3D11_COLOR_WRITE_ENABLE_ALL = 0xF;

    public const int D3D11_BLEND_ZERO = 1;
    public const int D3D11_BLEND_ONE = 2;
    public const int D3D11_BLEND_SRC_COLOR = 3;
    public const int D3D11_BLEND_INV_SRC_COLOR = 4;
    public const int D3D11_BLEND_SRC_ALPHA = 5;
    public const int D3D11_BLEND_INV_SRC_ALPHA = 6;
    public const int D3D11_BLEND_DEST_ALPHA = 7;
    public const int D3D11_BLEND_INV_DEST_ALPHA = 8;
    public const int D3D11_BLEND_DEST_COLOR = 9;
    public const int D3D11_BLEND_INV_DEST_COLOR = 10;

    public const int D3D11_BLEND_OP_ADD = 1;

    public const int D3D11_PRIMITIVE_TOPOLOGY_POINTLIST = 1;
    public const int D3D11_PRIMITIVE_TOPOLOGY_LINELIST = 2;
    public const int D3D11_PRIMITIVE_TOPOLOGY_LINESTRIP = 3;
    public const int D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST = 4;
    public const int D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP = 5;

    public const int DXGI_FORMAT_R8G8B8A8_UNORM = 28;
    public const int DXGI_FORMAT_B8G8R8A8_UNORM = 87;
    public const int DXGI_SWAP_EFFECT_FLIP_DISCARD = 4;
    public const int DXGI_SWAP_EFFECT_DISCARD = 0;
    public const uint DXGI_USAGE_RENDER_TARGET_OUTPUT = 0x20;

    public static readonly Guid IID_ID3D11Texture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
    public static readonly Guid IID_IDXGIFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_BUFFER_DESC
    {
        public uint ByteWidth;
        public int Usage;
        public uint BindFlags;
        public uint CPUAccessFlags;
        public uint MiscFlags;
        public uint StructureByteStride;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_SUBRESOURCE_DATA
    {
        public void* pSysMem;
        public uint SysMemPitch;
        public uint SysMemSlicePitch;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_TEXTURE2D_DESC
    {
        public uint Width;
        public uint Height;
        public uint MipLevels;
        public uint ArraySize;
        public int Format;
        public uint SampleDescCount;
        public uint SampleDescQuality;
        public int Usage;
        public uint BindFlags;
        public uint CPUAccessFlags;
        public uint MiscFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_INPUT_ELEMENT_DESC
    {
        public nint SemanticName; // Pointer to ANSI string
        public uint SemanticIndex;
        public int Format;
        public uint InputSlot;
        public uint AlignedByteOffset;
        public int InputSlotClass; // 0 = D3D11_INPUT_PER_VERTEX_DATA
        public uint InstanceDataStepRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_RASTERIZER_DESC
    {
        public int FillMode;
        public int CullMode;
        public int FrontCounterClockwise;
        public int DepthBias;
        public float DepthBiasClamp;
        public float SlopeScaledDepthBias;
        public int DepthClipEnable;
        public int ScissorEnable;
        public int MultisampleEnable;
        public int AntialiasedLineEnable;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_RENDER_TARGET_BLEND_DESC
    {
        public int BlendEnable;
        public int SrcBlend;
        public int DestBlend;
        public int BlendOp;
        public int SrcBlendAlpha;
        public int DestBlendAlpha;
        public int BlendOpAlpha;
        public byte RenderTargetWriteMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_BLEND_DESC
    {
        public int AlphaToCoverageEnable;
        public int IndependentBlendEnable;
        public D3D11_RENDER_TARGET_BLEND_DESC RenderTarget0;
        public D3D11_RENDER_TARGET_BLEND_DESC RenderTarget1;
        public D3D11_RENDER_TARGET_BLEND_DESC RenderTarget2;
        public D3D11_RENDER_TARGET_BLEND_DESC RenderTarget3;
        public D3D11_RENDER_TARGET_BLEND_DESC RenderTarget4;
        public D3D11_RENDER_TARGET_BLEND_DESC RenderTarget5;
        public D3D11_RENDER_TARGET_BLEND_DESC RenderTarget6;
        public D3D11_RENDER_TARGET_BLEND_DESC RenderTarget7;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_DEPTH_STENCILOP_DESC
    {
        public int StencilFailOp;
        public int StencilDepthFailOp;
        public int StencilPassOp;
        public int StencilFunc;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_DEPTH_STENCIL_DESC
    {
        public int DepthEnable;
        public int DepthWriteMask;
        public int DepthFunc;
        public int StencilEnable;
        public byte StencilReadMask;
        public byte StencilWriteMask;
        public D3D11_DEPTH_STENCILOP_DESC FrontFace;
        public D3D11_DEPTH_STENCILOP_DESC BackFace;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_VIEWPORT
    {
        public float TopLeftX;
        public float TopLeftY;
        public float Width;
        public float Height;
        public float MinDepth;
        public float MaxDepth;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_MODE_DESC
    {
        public uint Width;
        public uint Height;
        public DXGI_RATIONAL RefreshRate;
        public int Format;
        public int ScanlineOrdering;
        public int Scaling;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_SAMPLE_DESC
    {
        public uint Count;
        public uint Quality;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_SWAP_CHAIN_DESC
    {
        public DXGI_MODE_DESC BufferDesc;
        public DXGI_SAMPLE_DESC SampleDesc;
        public uint BufferUsage;
        public uint BufferCount;
        public nint OutputWindow;
        public int Windowed;
        public int SwapEffect;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DXGI_ADAPTER_DESC1
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
    public static extern int D3D11CreateDevice(
        nint pAdapter,
        int driverType,
        nint software,
        uint flags,
        nint pFeatureLevels,
        uint featureLevels,
        uint sdkVersion,
        out nint ppDevice,
        out int pFeatureLevel,
        out nint ppImmediateContext);

    [DllImport(D3D11Dll, SetLastError = true)]
    public static extern int D3D11CreateDeviceAndSwapChain(
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
    public static extern int CreateDXGIFactory1(in Guid riid, out nint ppFactory);

    [DllImport(D3DCompilerDll, SetLastError = true)]
    public static extern int D3DCompile(
        void* pSrcData,
        nuint srcDataSize,
        [MarshalAs(UnmanagedType.LPStr)] string? pSourceName,
        void* pDefines,
        void* pInclude,
        [MarshalAs(UnmanagedType.LPStr)] string pEntrypoint,
        [MarshalAs(UnmanagedType.LPStr)] string pTarget,
        uint flags1,
        uint flags2,
        out nint ppCode,
        out nint ppErrorMsgs);

    public static void SafeRelease(ref nint pComPtr)
    {
        if (pComPtr != nint.Zero)
        {
            var vtable = *(void***)pComPtr;
            var releaseFn = (delegate* unmanaged[Stdcall]<nint, uint>)vtable[2];
            releaseFn(pComPtr);
            pComPtr = nint.Zero;
        }
    }
}
