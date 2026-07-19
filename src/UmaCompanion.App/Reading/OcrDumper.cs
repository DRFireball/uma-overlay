using System.IO;
using Windows.Graphics.Imaging;

namespace UmaCompanion.App.Reading;

/// <summary>
/// Debug aid for tuning interpreters against real game art: when the UMA_OCR_DUMP env var is
/// set, every DISTINCT OCR'd screen is written as a timestamped .txt (the recognised lines) and
/// .png (the captured frame) pair under %LOCALAPPDATA%\UmaCompanion\ocr-dumps.
/// </summary>
public sealed class OcrDumper
{
    private readonly string _directory;
    private string? _lastKey;

    public OcrDumper(string directory)
    {
        _directory = directory;
    }

    /// <summary>A dumper when UMA_OCR_DUMP is set in the environment, else null.</summary>
    public static OcrDumper? FromEnvironment()
    {
        return string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UMA_OCR_DUMP"))
            ? null
            : new OcrDumper(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UmaCompanion", "ocr-dumps"));
    }

    /// <summary>Writes the pair if this screen's text differs from the previous dump.</summary>
    public async Task DumpAsync(IReadOnlyList<string> lines, SoftwareBitmap bitmap)
    {
        var key = string.Join("\n", lines);
        if (key == _lastKey)
        {
            return;
        }
        _lastKey = key;

        try
        {
            Directory.CreateDirectory(_directory);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            await File.WriteAllLinesAsync(Path.Combine(_directory, stamp + ".txt"), lines);

            using var file = File.Create(Path.Combine(_directory, stamp + ".png"));
            using var stream = file.AsRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
        }
        catch (Exception)
        {
            // Dumping must never break the capture loop.
        }
    }
}
