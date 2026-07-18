using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime; // AsBuffer()
using UmaCompanion.App.Reading;
using UmaCompanion.Core.Reading;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace UmaCompanion.App.Tests;

/// <summary>
/// End-to-end test of the OCR half of the screen reader: renders result-screen text into a
/// bitmap, runs it through the REAL Windows OCR engine, and interprets the recognised lines.
/// Covers everything except the WGC capture itself (which needs a live window and is verified
/// against the running game).
/// </summary>
public class OcrPipelineTests
{
    [SkippableFact]
    public async Task RenderedResultScreen_ThroughRealOcr_YieldsWonRaceResult()
    {
        var reader = WindowsOcrReader.TryCreate();
        Skip.If(reader is null, "no Windows OCR language pack installed");

        using var rendered = RenderResultScreen("Japan Cup", "1st");
        using var bitmap = await ToSoftwareBitmapAsync(rendered);

        var lines = await reader!.ReadLinesAsync(bitmap);
        Assert.NotEmpty(lines);

        var interpreter = new RaceResultScreenInterpreter(
            new RaceNameMatcher(new[] { (1013, "Japan Cup"), (1005, "Satsuki Sho") }));
        var result = interpreter.Interpret(lines);

        Assert.NotNull(result);
        Assert.Equal(1013, result.RaceId);
        Assert.True(result.Won);
    }

    [SkippableFact]
    public async Task RenderedNonResultScreen_ThroughRealOcr_YieldsNothing()
    {
        var reader = WindowsOcrReader.TryCreate();
        Skip.If(reader is null, "no Windows OCR language pack installed");

        using var rendered = RenderResultScreen("Training", "Rest");
        using var bitmap = await ToSoftwareBitmapAsync(rendered);

        var lines = await reader!.ReadLinesAsync(bitmap);
        var interpreter = new RaceResultScreenInterpreter(
            new RaceNameMatcher(new[] { (1013, "Japan Cup") }));

        Assert.Null(interpreter.Interpret(lines));
    }

    /// <summary>Big black-on-white text, comfortably OCR-readable.</summary>
    private static Bitmap RenderResultScreen(string title, string placement)
    {
        var bmp = new Bitmap(900, 400);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        using var titleFont = new Font("Segoe UI", 48, System.Drawing.FontStyle.Bold);
        using var placeFont = new Font("Segoe UI", 40);
        g.DrawString(title, titleFont, Brushes.Black, 40, 60);
        g.DrawString(placement, placeFont, Brushes.Black, 40, 220);
        return bmp;
    }

    private static async Task<SoftwareBitmap> ToSoftwareBitmapAsync(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);

        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(ms.ToArray().AsBuffer());
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    }
}
