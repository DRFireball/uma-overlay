using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;

namespace UmaCompanion.App.Reading;

/// <summary>
/// Captures a window with Windows.Graphics.Capture and raises <see cref="FrameCaptured"/> with a
/// <see cref="SoftwareBitmap"/> at most once per throttle interval (frames in between are
/// discarded — OCR at a few fps is pointless). Purely passive: WGC is compositor-side screen
/// capture, the target window is never touched. The receiver owns each delivered bitmap.
/// </summary>
public sealed class WindowCaptureSource : IDisposable
{
    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);
        IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
    }

    // IID of Windows.Graphics.Capture.IGraphicsCaptureItem.
    private static readonly Guid GraphicsCaptureItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    private readonly IDirect3DDevice _device;
    private readonly Direct3D11CaptureFramePool _framePool;
    private readonly GraphicsCaptureSession _session;
    private readonly TimeSpan _interval;
    private SizeInt32 _size;
    private DateTime _lastProcessedUtc;

    /// <summary>A captured frame, throttled. The receiver must dispose the bitmap.</summary>
    public event Action<SoftwareBitmap>? FrameCaptured;

    /// <summary>The captured window went away (game closed).</summary>
    public event Action? Closed;

    /// <summary>Starts capturing the given window. Throws if capture cannot be created for it.</summary>
    public static WindowCaptureSource Create(IntPtr hwnd, TimeSpan interval)
    {
        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        var iid = GraphicsCaptureItemIid;
        var abi = interop.CreateForWindow(hwnd, ref iid);
        try
        {
            return new WindowCaptureSource(GraphicsCaptureItem.FromAbi(abi), interval);
        }
        finally
        {
            Marshal.Release(abi);
        }
    }

    private WindowCaptureSource(GraphicsCaptureItem item, TimeSpan interval)
    {
        _interval = interval;
        _device = Direct3D11DeviceFactory.Create();
        _size = item.Size;
        _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            _device, DirectXPixelFormat.B8G8R8A8UIntNormalized, numberOfBuffers: 2, _size);
        _framePool.FrameArrived += OnFrameArrived;
        item.Closed += (_, _) => Closed?.Invoke();
        _session = _framePool.CreateCaptureSession(item);
        _session.IsCursorCaptureEnabled = false;
        _session.StartCapture();
    }

    private async void OnFrameArrived(Direct3D11CaptureFramePool sender, object? args)
    {
        using var frame = sender.TryGetNextFrame();
        if (frame is null)
        {
            return;
        }

        // Window resized: recreate the pool at the new size and wait for the next frame.
        if (frame.ContentSize.Width != _size.Width || frame.ContentSize.Height != _size.Height)
        {
            _size = frame.ContentSize;
            sender.Recreate(_device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _size);
            return;
        }

        var now = DateTime.UtcNow;
        if (now - _lastProcessedUtc < _interval)
        {
            return;
        }
        _lastProcessedUtc = now;

        SoftwareBitmap bitmap;
        try
        {
            bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);
        }
        catch (Exception)
        {
            return; // device lost / surface gone mid-copy; the next frame will retry
        }
        FrameCaptured?.Invoke(bitmap);
    }

    public void Dispose()
    {
        _session.Dispose();
        _framePool.Dispose();
        _device.Dispose();
    }
}
