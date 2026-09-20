using System.IO;
using System.Windows.Media.Imaging;

namespace CodexSOS.App;

/// <summary>
/// Keeps screenshot input useful without turning image loading into an
/// unbounded decoder.  The source is inspected first, then reduced to a
/// bounded in-memory image when it is a reasonable large screenshot.
/// </summary>
public static class ScreenshotNormalizer
{
    public const long MaximumBytes = 25 * 1024 * 1024;
    public const int MaximumSourceDimension = 10_000;
    public const long MaximumSourcePixels = 30_000_000;
    public const int MaximumDimension = 4_096;
    public const long MaximumNormalizedPixels = 12_000_000;

    public static bool TryGetTargetSize(
        int sourceWidth,
        int sourceHeight,
        out int targetWidth,
        out int targetHeight,
        out bool resized)
    {
        targetWidth = 0;
        targetHeight = 0;
        resized = false;
        if (sourceWidth <= 0 || sourceHeight <= 0 ||
            sourceWidth > MaximumSourceDimension || sourceHeight > MaximumSourceDimension)
        {
            return false;
        }

        var sourcePixels = (long)sourceWidth * sourceHeight;
        if (sourcePixels > MaximumSourcePixels)
        {
            return false;
        }

        var scale = Math.Min(1d, Math.Min(
            MaximumDimension / (double)sourceWidth,
            MaximumDimension / (double)sourceHeight));
        if (sourcePixels > MaximumNormalizedPixels)
        {
            scale = Math.Min(scale, Math.Sqrt(MaximumNormalizedPixels / (double)sourcePixels));
        }

        targetWidth = Math.Max(1, (int)Math.Floor(sourceWidth * scale));
        targetHeight = Math.Max(1, (int)Math.Floor(sourceHeight * scale));
        resized = targetWidth != sourceWidth || targetHeight != sourceHeight;
        return true;
    }

    public static bool TryNormalize(
        BitmapSource source,
        out BitmapSource normalized,
        out bool resized)
    {
        normalized = null!;
        resized = false;
        if (source is null || !TryGetTargetSize(
                source.PixelWidth,
                source.PixelHeight,
                out var targetWidth,
                out var targetHeight,
                out resized))
        {
            return false;
        }

        if (!resized)
        {
            source.Freeze();
            normalized = source;
            return true;
        }

        var transformed = new TransformedBitmap(
            source,
            new System.Windows.Media.ScaleTransform(
                targetWidth / (double)source.PixelWidth,
                targetHeight / (double)source.PixelHeight));
        transformed.Freeze();
        normalized = transformed;
        return true;
    }

    public static bool TryReadMetadata(
        string path,
        out int width,
        out int height)
    {
        width = 0;
        height = 0;
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.None);
            var frame = decoder.Frames.FirstOrDefault();
            if (frame is null) return false;
            width = frame.PixelWidth;
            height = frame.PixelHeight;
            return width > 0 && height > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                   NotSupportedException or ArgumentException or
                                   InvalidDataException or EndOfStreamException or
                                   OutOfMemoryException)
        {
            return false;
        }
    }
}
