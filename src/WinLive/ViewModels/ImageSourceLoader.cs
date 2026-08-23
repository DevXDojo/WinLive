using HeyRed.ImageSharp.Heif.Formats.Heif;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;
using Windows.Storage.Streams;

namespace WinLive.ViewModels;

/// <summary>Decodes HEIC through the bundled libheif runtime instead of Windows codec extensions.</summary>
internal static class ImageSourceLoader
{
    public static async Task<ImageSource?> LoadAsync(string path, int maxPixelWidth, CancellationToken cancellationToken = default)
    {
        if (!Path.GetExtension(path).Equals(".heic", StringComparison.OrdinalIgnoreCase))
        {
            try { return new BitmapImage(new Uri(path)) { DecodePixelWidth = maxPixelWidth }; }
            catch (UriFormatException) { return null; }
        }

        try
        {
            var png = await Task.Run(() => DecodeHeicToPng(path, maxPixelWidth, cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = new InMemoryRandomAccessStream();
            using var writer = new DataWriter(stream);
            writer.WriteBytes(png);
            await writer.StoreAsync();
            stream.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return null; }
    }

    private static byte[] DecodeHeicToPng(string path, int maxPixelWidth, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = new DecoderOptions { Configuration = new Configuration(new HeifConfigurationModule()) };
        using var image = Image.Load(options, path);
        image.Mutate(context => context.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(maxPixelWidth, maxPixelWidth),
        }));
        using var buffer = new MemoryStream();
        image.SaveAsPng(buffer);
        return buffer.ToArray();
    }
}
