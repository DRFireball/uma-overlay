using Windows.Graphics.Imaging;
using UmaCompanion.Core.Live;
using UmaCompanion.Core.Reading;

namespace UmaCompanion.App.Reading;

/// <summary>
/// The passive screen-reader frame producer: polls for the game window, captures it via
/// <see cref="WindowCaptureSource"/>, OCRs frames with <see cref="WindowsOcrReader"/>, and runs
/// the Core interpreters over the text. Recognised game state is emitted as the same
/// <see cref="GameFrame"/>s the WebSocket ingest produces, so downstream consumers cannot tell
/// the two sources apart. Emits each distinct result once per continuous on-screen appearance.
/// </summary>
public sealed class ScreenReaderService : IDisposable
{
    private static readonly TimeSpan SearchInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan CaptureInterval = TimeSpan.FromSeconds(1);

    private readonly RaceResultScreenInterpreter _interpreter;
    private readonly Action<GameFrame> _onFrame;
    private readonly Action<string> _onStatus;
    private readonly WindowsOcrReader? _ocr;
    private readonly Timer _searchTimer;
    private readonly object _gate = new();

    private WindowCaptureSource? _capture;
    private RaceResult? _lastEmitted;
    private int _busy;
    private bool _disposed;

    public ScreenReaderService(
        RaceResultScreenInterpreter interpreter, Action<GameFrame> onFrame, Action<string> onStatus)
    {
        _interpreter = interpreter;
        _onFrame = onFrame;
        _onStatus = onStatus;
        _ocr = WindowsOcrReader.TryCreate();
        _searchTimer = new Timer(_ => SearchTick());
    }

    public void Start()
    {
        if (_ocr is null)
        {
            _onStatus("Screen reader off — no OCR language pack installed.");
            return;
        }
        _searchTimer.Change(TimeSpan.Zero, SearchInterval);
    }

    private void SearchTick()
    {
        lock (_gate)
        {
            if (_disposed || _capture is not null)
            {
                return;
            }

            var hwnd = GameWindowLocator.FindGameWindow();
            if (hwnd == IntPtr.Zero)
            {
                _onStatus("Screen reader: waiting for the game window…");
                return;
            }

            try
            {
                var capture = WindowCaptureSource.Create(hwnd, CaptureInterval);
                capture.FrameCaptured += OnBitmap;
                capture.Closed += OnCaptureClosed;
                _capture = capture;
                _onStatus("Screen reader: capturing the game window.");
            }
            catch (Exception ex)
            {
                _onStatus($"Screen reader: capture failed — {ex.Message}");
            }
        }
    }

    private void OnCaptureClosed()
    {
        lock (_gate)
        {
            _capture?.Dispose();
            _capture = null;
            _lastEmitted = null;
        }
        if (!_disposed)
        {
            _onStatus("Screen reader: game window closed — waiting…");
        }
    }

    private async void OnBitmap(SoftwareBitmap bitmap)
    {
        // One OCR at a time; frames arriving while busy are dropped, never queued.
        if (Interlocked.Exchange(ref _busy, 1) == 1)
        {
            bitmap.Dispose();
            return;
        }
        try
        {
            IReadOnlyList<string> lines;
            using (bitmap)
            {
                lines = await _ocr!.ReadLinesAsync(bitmap);
            }

            var result = _interpreter.Interpret(lines);
            if (result is null)
            {
                _lastEmitted = null; // result screen gone; the next one may be the same race again
                return;
            }
            if (result == _lastEmitted)
            {
                return; // same screen still showing
            }
            _lastEmitted = result;
            _onFrame(new RaceResultsFrame(new[] { result }));
        }
        catch (Exception)
        {
            // One bad frame (OCR hiccup, device loss) must not kill the reader loop.
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _searchTimer.Dispose();
            _capture?.Dispose();
            _capture = null;
        }
    }
}
