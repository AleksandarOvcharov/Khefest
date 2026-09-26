using System.Runtime.InteropServices;
using Khefest.Core.Errors;
using Khefest.Core.Logging;

namespace Khefest.Graphics.Texturing;

/// <summary>
/// Native Windows Imaging Component (WIC) image decoder.
/// Decodes PNG, JPEG, BMP, and TIFF files into 32-bit RGBA pixel buffers without external dependencies.
/// </summary>
public static unsafe class WicImageDecoder
{
    private static readonly ILogger Logger = LogManager.GetLogger(KhefestSubsystem.Graphics, "WicImageDecoder");

    // GUID_WICPixelFormat32bppRGBA = {f5c7ad2d-6a8d-43dd-a7a0-7d7a1f421710}
    private static readonly Guid GUID_WICPixelFormat32bppRGBA = new("f5c7ad2d-6a8d-43dd-a7a0-7d7a1f421710");
    // GUID_WICPixelFormat32bppBGRA = {6fddc324-4e03-4bfe-b185-3d77768dc90c}
    private static readonly Guid GUID_WICPixelFormat32bppBGRA = new("6fddc324-4e03-4bfe-b185-3d77768dc90c");

    private const uint WINCODEC_SDK_VERSION1 = 0x0236;
    private const uint WINCODEC_SDK_VERSION2 = 0x0237;

    [DllImport("WindowsCodecs.dll", EntryPoint = "WICCreateImagingFactory_Proxy", ExactSpelling = true)]
    private static extern int WICCreateImagingFactory_Proxy(uint sdkVersion, out nint ppIImagingFactory);

    [DllImport("WindowsCodecs.dll", EntryPoint = "WICConvertBitmapSource", ExactSpelling = true)]
    private static extern int WICConvertBitmapSource(in Guid dstFormat, nint pISrc, out nint ppIDst);

    public readonly struct DecodedImage
    {
        public readonly int Width;
        public readonly int Height;
        public readonly byte[] Pixels;

        public DecodedImage(int width, int height, byte[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }
    }

    /// <summary>
    /// Decodes an image file from disk into a 32-bit RGBA pixel buffer.
    /// </summary>
    public static DecodedImage DecodeFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Image file not found: {filePath}", filePath);
        }

        nint pFactory = nint.Zero;
        nint pDecoder = nint.Zero;
        nint pFrame = nint.Zero;
        nint pConverter = nint.Zero;

        try
        {
            // 1. Create IWICImagingFactory via native export
            int hr = WICCreateImagingFactory_Proxy(WINCODEC_SDK_VERSION2, out pFactory);
            if (hr < 0 || pFactory == nint.Zero)
            {
                hr = WICCreateImagingFactory_Proxy(WINCODEC_SDK_VERSION1, out pFactory);
            }

            if (hr < 0 || pFactory == nint.Zero)
            {
                throw new KhefestGraphicsException(
                    KhefestErrorCode.GraphicsDeviceCreationFailed,
                    $"Failed to create WIC Imaging Factory (0x{hr:X8}).");
            }

            var factoryVTable = *(void***)pFactory;

            // 2. CreateDecoderFromFilename: slot 3 on IWICImagingFactory
            var createDecoderFn = (delegate* unmanaged[Stdcall]<nint, char*, void*, uint, uint, nint*, int>)factoryVTable[3];
            fixed (char* pPath = filePath)
            {
                hr = createDecoderFn(pFactory, pPath, null, 0x80000000 /* GENERIC_READ */, 0 /* WICDecodeMetadataCacheOnDemand */, &pDecoder);
            }

            if (hr < 0 || pDecoder == nint.Zero)
            {
                throw new KhefestGraphicsException(
                    KhefestErrorCode.AssetLoadFailed,
                    $"Failed to load image '{filePath}' using WIC (0x{hr:X8}).");
            }

            var decoderVTable = *(void***)pDecoder;

            // 3. GetFrame(0): slot 13 on IWICBitmapDecoder
            var getFrameFn = (delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)decoderVTable[13];
            hr = getFrameFn(pDecoder, 0, &pFrame);
            if (hr < 0 || pFrame == nint.Zero)
            {
                throw new KhefestGraphicsException(
                    KhefestErrorCode.AssetLoadFailed,
                    $"Failed to get image frame from '{filePath}' (0x{hr:X8}).");
            }

            var frameVTable = *(void***)pFrame;

            // 4. GetSize: slot 3 on IWICBitmapSource
            uint width = 0, height = 0;
            var getSizeFn = (delegate* unmanaged[Stdcall]<nint, uint*, uint*, int>)frameVTable[3];
            hr = getSizeFn(pFrame, &width, &height);
            if (hr < 0 || width == 0 || height == 0)
            {
                throw new KhefestGraphicsException(
                    KhefestErrorCode.AssetLoadFailed,
                    $"Failed to get image dimensions for '{filePath}' (0x{hr:X8}).");
            }

            // 5. Convert format automatically to 32bppRGBA (or fallback to 32bppBGRA)
            bool isBgra = false;
            hr = WICConvertBitmapSource(in GUID_WICPixelFormat32bppRGBA, pFrame, out pConverter);
            if (hr < 0 || pConverter == nint.Zero)
            {
                hr = WICConvertBitmapSource(in GUID_WICPixelFormat32bppBGRA, pFrame, out pConverter);
                isBgra = true;
            }

            if (hr < 0 || pConverter == nint.Zero)
            {
                throw new KhefestGraphicsException(
                    KhefestErrorCode.AssetLoadFailed,
                    $"Failed to convert image format to 32bpp RGBA or BGRA (0x{hr:X8}).");
            }

            var converterVTable = *(void***)pConverter;

            // 6. CopyPixels: slot 7 on IWICBitmapSource
            uint stride = width * 4;
            uint bufferSize = stride * height;
            var pixelBuffer = new byte[bufferSize];

            fixed (byte* pBuf = pixelBuffer)
            {
                var copyPixelsFn = (delegate* unmanaged[Stdcall]<nint, void*, uint, uint, byte*, int>)converterVTable[7];
                hr = copyPixelsFn(pConverter, null, stride, bufferSize, pBuf);
                if (hr < 0)
                {
                    throw new KhefestGraphicsException(
                        KhefestErrorCode.AssetLoadFailed,
                        $"Failed to copy image pixels for '{filePath}' (0x{hr:X8}).");
                }
            }

            if (isBgra)
            {
                for (int i = 0; i < bufferSize; i += 4)
                {
                    byte b = pixelBuffer[i + 0];
                    pixelBuffer[i + 0] = pixelBuffer[i + 2];
                    pixelBuffer[i + 2] = b;
                }
            }

            Logger.Info($"Decoded image '{filePath}' ({width}x{height}, 32bpp RGBA, {bufferSize / 1024} KB)");
            return new DecodedImage((int)width, (int)height, pixelBuffer);
        }
        finally
        {
            if (pConverter != nint.Zero)
            {
                var vtbl = *(void***)pConverter;
                var releaseFn = (delegate* unmanaged[Stdcall]<nint, uint>)vtbl[2];
                releaseFn(pConverter);
            }

            if (pFrame != nint.Zero)
            {
                var vtbl = *(void***)pFrame;
                var releaseFn = (delegate* unmanaged[Stdcall]<nint, uint>)vtbl[2];
                releaseFn(pFrame);
            }

            if (pDecoder != nint.Zero)
            {
                var vtbl = *(void***)pDecoder;
                var releaseFn = (delegate* unmanaged[Stdcall]<nint, uint>)vtbl[2];
                releaseFn(pDecoder);
            }

            if (pFactory != nint.Zero)
            {
                var vtbl = *(void***)pFactory;
                var releaseFn = (delegate* unmanaged[Stdcall]<nint, uint>)vtbl[2];
                releaseFn(pFactory);
            }
        }
    }
}
