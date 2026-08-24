using System.Windows.Media.Imaging;
using System.Windows.Media;
using TesserNet;

namespace CodexSOS.App.Services;

public interface ILocalOcrService
{
    string Name { get; }
    bool IsAvailable { get; }
    Task<string> ReadAsync(BitmapSource image, CancellationToken cancellationToken);
}

public sealed class UnavailableOcrService : ILocalOcrService
{
    public string Name => "不可用";
    public bool IsAvailable => false;
    public Task<string> ReadAsync(BitmapSource image, CancellationToken cancellationToken) =>
        Task.FromResult(string.Empty);
}

public sealed class TesserNetOcrService : ILocalOcrService
{
    private static readonly TimeSpan OcrLimit = TimeSpan.FromSeconds(12);
    private const int MaximumDimension = 4096;

    public string Name => "本机英文识字";
    public bool IsAvailable => true;

    public async Task<string> ReadAsync(BitmapSource image, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var prepared = Prepare(image);
        var pixels = new byte[prepared.Stride * prepared.Height];
        prepared.Source.CopyPixels(pixels, prepared.Stride, 0);

        var readTask = Task.Run(() =>
        {
            using var engine = new Tesseract();
            return engine.Read(pixels, prepared.Width, prepared.Height, 4);
        }, CancellationToken.None);
        var winner = await Task.WhenAny(readTask, Task.Delay(OcrLimit, cancellationToken)).ConfigureAwait(false);
        if (winner != readTask)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("本机识字超过安全等待时间。");
        }

        var text = await readTask.ConfigureAwait(false);
        return text.Length > 20_000 ? text[..20_000] : text;
    }

    private static PreparedImage Prepare(BitmapSource source)
    {
        BitmapSource resized = source;
        if (source.PixelWidth > MaximumDimension || source.PixelHeight > MaximumDimension)
        {
            var scale = Math.Min((double)MaximumDimension / source.PixelWidth,
                (double)MaximumDimension / source.PixelHeight);
            var transform = new ScaleTransform(scale, scale);
            var transformed = new TransformedBitmap(source, transform);
            transformed.Freeze();
            resized = transformed;
        }

        BitmapSource bgra = resized;
        if (resized.Format != PixelFormats.Bgra32)
        {
            var converted = new FormatConvertedBitmap(resized, PixelFormats.Bgra32, null, 0);
            converted.Freeze();
            bgra = converted;
        }

        return new PreparedImage(bgra, bgra.PixelWidth, bgra.PixelHeight, bgra.PixelWidth * 4);
    }

    private sealed record PreparedImage(BitmapSource Source, int Width, int Height, int Stride);
}
