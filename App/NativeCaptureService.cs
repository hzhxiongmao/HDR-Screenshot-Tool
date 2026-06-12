using System.Runtime.InteropServices;
using WpfRect = System.Windows.Rect;

namespace HDRScreenshotTool;

public sealed class NativeCaptureService : IDisposable
{
    private bool _initialized;

    public NativeCaptureService()
    {
        _initialized = CaptureInitialize();
    }

    public bool IsInitialized => _initialized;

    public unsafe (byte[] pixels, int w, int h) Capture(WpfRect physicalRegion, IntPtr hMonitor)
    {
        int x = (int)physicalRegion.Left;
        int y = (int)physicalRegion.Top;
        int w = (int)physicalRegion.Width;
        int h = (int)physicalRegion.Height;

        byte* ptr = null;
        int outW = 0, outH = 0;

        if (!CaptureScreen(x, y, w, h, hMonitor, &ptr, &outW, &outH) || ptr == null)
        {
            var errPtr = CaptureGetLastError();
            var err = errPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(errPtr) : "unknown";
            throw new InvalidOperationException($"Native capture failed: {err}");
        }

        var pixels = new byte[outW * outH * 4];
        fixed (byte* dst = pixels)
        {
            Buffer.MemoryCopy(ptr, dst, pixels.Length, pixels.Length);
        }
        CaptureFreePixels(ptr);
        return (pixels, outW, outH);
    }

    [DllImport("NativeCapture.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern bool CaptureInitialize();

    [DllImport("NativeCapture.dll", CallingConvention = CallingConvention.StdCall)]
    private static unsafe extern bool CaptureScreen(int x, int y, int w, int h, IntPtr monitor, byte** pixels, int* outW, int* outH);

    [DllImport("NativeCapture.dll", CallingConvention = CallingConvention.StdCall)]
    private static unsafe extern void CaptureFreePixels(byte* pixels);

    [DllImport("NativeCapture.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr CaptureGetLastError();

    [DllImport("NativeCapture.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void CaptureShutdown();

    public string GetDebugInfo() => _initialized ? "WGC Native" : "WGC Failed";

    public void Dispose()
    {
        if (_initialized) { CaptureShutdown(); _initialized = false; }
    }
}
