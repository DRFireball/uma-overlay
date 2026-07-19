using System.Runtime.InteropServices;
using UmaCompanion.Core.Reading;
using Windows.Graphics.Imaging;
using WinRT;

namespace UmaCompanion.App.Reading;

/// <summary>
/// Samples a captured frame's pixels: mean luminance (0 black … 255 white) over a region.
/// Feeds <see cref="UmaCompanion.Core.Reading.TrophyRoomScreenInterpreter"/>'s owned-vs-silhouette
/// discrimination.
/// </summary>
public static class BitmapBrightnessProbe
{
    [ComImport, Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    /// <summary>Mean luminance of <paramref name="region"/> (clamped to the bitmap; 0 if empty).</summary>
    public static unsafe double MeanLuminance(SoftwareBitmap bitmap, OcrRect region)
    {
        if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
        {
            using var converted = SoftwareBitmap.Convert(
                bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            return MeanLuminance(converted, region);
        }

        var left = Math.Max(0, (int)region.X);
        var top = Math.Max(0, (int)region.Y);
        var right = Math.Min(bitmap.PixelWidth, (int)(region.X + region.Width));
        var bottom = Math.Min(bitmap.PixelHeight, (int)(region.Y + region.Height));
        if (left >= right || top >= bottom)
        {
            return 0;
        }

        using var buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read);
        using var reference = buffer.CreateReference();
        reference.As<IMemoryBufferByteAccess>().GetBuffer(out var data, out _);
        var plane = buffer.GetPlaneDescription(0);

        double sum = 0;
        var count = 0;
        for (var y = top; y < bottom; y += 2) // every 2nd pixel is plenty for a mean
        {
            var row = data + plane.StartIndex + y * plane.Stride;
            for (var x = left; x < right; x += 2)
            {
                var px = row + x * 4; // Bgra8
                sum += 0.114 * px[0] + 0.587 * px[1] + 0.299 * px[2];
                count++;
            }
        }
        return count == 0 ? 0 : sum / count;
    }
}
