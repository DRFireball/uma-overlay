using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using UmaCompanion.App.Reading;
using Windows.Graphics.Imaging;

namespace UmaCompanion.App.Tests;

/// <summary>
/// Executes the full WGC capture stack (D3D device interop → GraphicsCaptureItem → frame pool →
/// SoftwareBitmap) against a real window this test briefly opens. This is the only automated
/// coverage of the raw COM/WinRT interop; a short-lived small window (with the OS's capture
/// border) flashing during the run is expected.
/// </summary>
public class WindowCaptureSourceTests
{
    [SkippableFact]
    public async Task Capture_OfARealWindow_DeliversAFrame()
    {
        Skip.If(!Environment.UserInteractive, "WGC needs an interactive desktop session");

        var hwnd = IntPtr.Zero;
        Dispatcher? dispatcher = null;
        var windowReady = new ManualResetEventSlim();

        var uiThread = new Thread(() =>
        {
            var window = new Window
            {
                Title = "UmaCompanion capture self-test",
                Width = 320,
                Height = 200,
                ShowInTaskbar = false,
                ShowActivated = false,
                Background = Brushes.White,
                Content = new TextBlock { Text = "Capture Test", FontSize = 32 },
            };
            window.Show();
            hwnd = new WindowInteropHelper(window).Handle;
            dispatcher = Dispatcher.CurrentDispatcher;
            windowReady.Set();
            Dispatcher.Run(); // pumps until InvokeShutdown below
        });
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.IsBackground = true;
        uiThread.Start();

        Assert.True(windowReady.Wait(TimeSpan.FromSeconds(5)), "test window failed to open");
        try
        {
            var frameReceived = new TaskCompletionSource<SoftwareBitmap>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var source = WindowCaptureSource.Create(hwnd, interval: TimeSpan.Zero);
            source.FrameCaptured += bitmap => frameReceived.TrySetResult(bitmap);

            using var frame = await frameReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(frame.PixelWidth > 0 && frame.PixelHeight > 0);
        }
        finally
        {
            dispatcher?.InvokeShutdown();
        }
    }
}
