using UmaCompanion.Core.Reading;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using OcrLine = UmaCompanion.Core.Reading.OcrLine; // not Windows.Media.Ocr.OcrLine

namespace UmaCompanion.App.Reading;

/// <summary>
/// Thin wrapper over the in-box Windows OCR engine: SoftwareBitmap in, positioned text lines
/// out (coordinates always in the ORIGINAL bitmap's pixel space). Oversized bitmaps (a game
/// window on a 4K display can exceed the engine's limit) are downscaled to fit before
/// recognition, and their line positions mapped back.
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
        => (await ReadAsync(bitmap)).Select(l => l.Text).ToList();

    /// <summary>
    /// Recognises at BOTH native resolution and 2× zoom, merged. Game labels are ~18 px tall at
    /// 1080p — small enough that the engine silently drops whole rows at native scale, while the
    /// 2× pass occasionally garbles a label the native pass read cleanly. Neither wins alone;
    /// together they cover each other, and downstream matching dedups by race. Frames beyond the
    /// engine's size limit are downscaled to fit for the base pass.
    /// </summary>
    public async Task<IReadOnlyList<OcrLine>> ReadAsync(SoftwareBitmap bitmap)
    {
        var max = (int)OcrEngine.MaxImageDimension;
        var longest = Math.Max(bitmap.PixelWidth, bitmap.PixelHeight);
        var fitScale = Math.Min(1.0, (double)max / longest);
        var zoomScale = Math.Min(2.0, (double)max / longest);

        var lines = new List<OcrLine>(await ReadAtScaleAsync(bitmap, fitScale));
        if (zoomScale - fitScale > 0.01)
        {
            lines.AddRange(await ReadAtScaleAsync(bitmap, zoomScale));
        }
        return lines;
    }

    private async Task<IReadOnlyList<OcrLine>> ReadAtScaleAsync(SoftwareBitmap bitmap, double scale)
    {
        var scaled = Math.Abs(scale - 1.0) < 0.01 ? null : await RescaleAsync(bitmap, scale);
        try
        {
            var target = scaled ?? bitmap;
            // Map recognised positions back into the original bitmap's pixel space.
            var factor = (double)bitmap.PixelWidth / target.PixelWidth;
            var result = await _engine.RecognizeAsync(target);

            var lines = new List<OcrLine>();
            foreach (var line in result.Lines)
            {
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;
                foreach (var word in line.Words)
                {
                    var r = word.BoundingRect;
                    minX = Math.Min(minX, r.X);
                    minY = Math.Min(minY, r.Y);
                    maxX = Math.Max(maxX, r.X + r.Width);
                    maxY = Math.Max(maxY, r.Y + r.Height);
                }
                if (minX > maxX)
                {
                    continue; // line with no words
                }
                lines.Add(new OcrLine(line.Text, new OcrRect(
                    minX * factor, minY * factor,
                    (maxX - minX) * factor, (maxY - minY) * factor)));
            }
            return lines;
        }
        finally
        {
            scaled?.Dispose();
        }
    }

    private static async Task<SoftwareBitmap> RescaleAsync(SoftwareBitmap bitmap, double scale)
    {
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(bitmap);
        encoder.BitmapTransform.ScaledWidth = (uint)(bitmap.PixelWidth * scale);
        encoder.BitmapTransform.ScaledHeight = (uint)(bitmap.PixelHeight * scale);
        encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
        await encoder.FlushAsync();

        var decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    }
}
