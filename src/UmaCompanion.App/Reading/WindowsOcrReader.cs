using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace UmaCompanion.App.Reading;

/// <summary>
/// Thin wrapper over the in-box Windows OCR engine: SoftwareBitmap in, text lines out.
/// Oversized bitmaps (a game window on a 4K display can exceed the engine's limit) are
/// downscaled to fit before recognition.
/// </summary>
public sealed class WindowsOcrReader
{
    private readonly OcrEngine _engine;

    private WindowsOcrReader(OcrEngine engine)
    {
        _engine = engine;
    }

    /// <summary>An English (preferred) or user-language OCR engine, or null if none is installed.</summary>
    public static WindowsOcrReader? TryCreate()
    {
        var engine = OcrEngine.TryCreateFromLanguage(new Language("en-US"))
                     ?? OcrEngine.TryCreateFromUserProfileLanguages();
        return engine is null ? null : new WindowsOcrReader(engine);
    }

    public async Task<IReadOnlyList<string>> ReadLinesAsync(SoftwareBitmap bitmap)
    {
        var scaled = await ScaleToFitAsync(bitmap, (int)OcrEngine.MaxImageDimension);
        try
        {
            var result = await _engine.RecognizeAsync(scaled ?? bitmap);
            return result.Lines.Select(l => l.Text).ToList();
        }
        finally
        {
            scaled?.Dispose();
        }
    }

    /// <summary>A downscaled copy when the bitmap exceeds the OCR size limit, else null.</summary>
    private static async Task<SoftwareBitmap?> ScaleToFitAsync(SoftwareBitmap bitmap, int maxDimension)
    {
        int width = bitmap.PixelWidth, height = bitmap.PixelHeight;
        if (width <= maxDimension && height <= maxDimension)
        {
            return null;
        }

        var scale = Math.Min((double)maxDimension / width, (double)maxDimension / height);
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(bitmap);
        encoder.BitmapTransform.ScaledWidth = (uint)(width * scale);
        encoder.BitmapTransform.ScaledHeight = (uint)(height * scale);
        encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
        await encoder.FlushAsync();

        var decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    }
}
