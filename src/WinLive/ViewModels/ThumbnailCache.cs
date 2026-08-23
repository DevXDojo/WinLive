using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using WinLive.Core;
using System.Security.Cryptography;
using System.Text;

namespace WinLive.ViewModels;

/// <summary>
/// Decodes only a bounded number of visible thumbnails at once. SetSourceAsync keeps
/// the Windows image pipeline used by the viewer, but exposes the real decode task so
/// the semaphore limits decoding rather than merely BitmapImage construction.
/// </summary>
internal static class ThumbnailCache
{
    private const int DecodeWidth = 320;
    private const long MaxDiskCacheBytes = 512L * 1024 * 1024;
    private static readonly SemaphoreSlim GridDecodeGate = new(1, 1);
    private static readonly SemaphoreSlim ViewerDecodeGate = new(1, 1);
    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, ImageSource> Cache = new(StringComparer.Ordinal);
    private static readonly Queue<string> EvictionOrder = new();

    public static async Task<ImageSource?> GetAsync(MediaItem item, CancellationToken cancellationToken, ThumbnailPriority priority = ThumbnailPriority.Grid)
    {
        lock (CacheLock)
        {
            if (Cache.TryGetValue(item.Id, out var cached)) return cached;
        }

        var gate = priority == ThumbnailPriority.Viewer ? ViewerDecodeGate : GridDecodeGate;
        await gate.WaitAsync(cancellationToken);
        try
        {
            lock (CacheLock)
            {
                if (Cache.TryGetValue(item.Id, out var cached)) return cached;
            }

            var cachePath = GetCachePath(item.Id);
            var bitmap = await LoadCachedThumbnailAsync(cachePath, cancellationToken)
                ?? await CreateAndCacheThumbnailAsync(item.PrimaryPath, cachePath, cancellationToken);
            if (bitmap is null) return null;

            lock (CacheLock)
            {
                while (Cache.Count >= 192 && EvictionOrder.TryDequeue(out var oldest)) Cache.Remove(oldest);
                Cache[item.Id] = bitmap;
                EvictionOrder.Enqueue(item.Id);
            }
            return bitmap;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) when (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException(cancellationToken); }
        catch (Exception) { return null; }
        finally { gate.Release(); }
    }

    public static void Clear()
    {
        lock (CacheLock)
        {
            Cache.Clear();
            EvictionOrder.Clear();
        }
    }

    private static async Task<ImageSource?> LoadCachedThumbnailAsync(string cachePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(cachePath)) return null;
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(cachePath).AsTask(cancellationToken);
            using var stream = await file.OpenReadAsync().AsTask(cancellationToken);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            File.SetLastAccessTimeUtc(cachePath, DateTime.UtcNow);
            return bitmap;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return null; }
    }

    private static async Task<ImageSource?> CreateAndCacheThumbnailAsync(string sourcePath, string cachePath, CancellationToken cancellationToken)
    {
        var file = await StorageFile.GetFileFromPathAsync(sourcePath).AsTask(cancellationToken);
        using var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.PicturesView, DecodeWidth, ThumbnailOptions.ResizeThumbnail).AsTask(cancellationToken);
        if (thumbnail is null) return await DecodeSourceAsync(file, cancellationToken);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            thumbnail.Seek(0);
            using var input = thumbnail.AsStreamForRead();
            await using var output = new FileStream(cachePath, FileMode.Create, FileAccess.Write, FileShare.None, 32 * 1024, useAsync: true);
            await input.CopyToAsync(output, cancellationToken);
            await output.FlushAsync(cancellationToken);
            _ = Task.Run(TrimDiskCache);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { /* The decoded thumbnail remains usable without a disk entry. */ }

        thumbnail.Seek(0);
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(thumbnail);
        return bitmap;
    }

    private static async Task<ImageSource?> DecodeSourceAsync(StorageFile file, CancellationToken cancellationToken)
    {
        using var stream = await file.OpenReadAsync().AsTask(cancellationToken);
        var bitmap = new BitmapImage { DecodePixelWidth = DecodeWidth };
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }

    private static string GetCachePath(string itemId)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(itemId)));
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinLive", "thumbs", $"{hash}.thumb");
    }

    private static void TrimDiskCache()
    {
        try
        {
            var directory = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinLive", "thumbs"));
            if (!directory.Exists) return;
            var files = directory.EnumerateFiles("*.thumb").OrderBy(file => file.LastAccessTimeUtc).ToArray();
            var total = files.Sum(file => file.Length);
            foreach (var file in files)
            {
                if (total <= MaxDiskCacheBytes) break;
                total -= file.Length;
                file.Delete();
            }
        }
        catch (Exception) { /* Cache trimming must never affect browsing. */ }
    }
}

internal enum ThumbnailPriority
{
    Grid,
    Viewer,
}
