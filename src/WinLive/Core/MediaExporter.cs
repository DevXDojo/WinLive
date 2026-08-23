namespace WinLive.Core;

public sealed record ExportResult(int MediaItems, int Files, long Bytes);

/// <summary>Creates independent copies in a user-selected destination; source media is never changed.</summary>
public sealed class MediaExporter
{
    public async Task<ExportResult> ExportAsync(
        IEnumerable<MediaItem> items,
        string destinationFolder,
        IProgress<(int Completed, int Total)>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFolder);
        Directory.CreateDirectory(destinationFolder);
        var media = items.DistinctBy(item => item.PrimaryPath, StringComparer.OrdinalIgnoreCase).ToArray();
        var sources = media.SelectMany(item => item.MotionPath is { } motionPath ? new[] { item.PrimaryPath, motionPath } : [item.PrimaryPath])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        long bytes = 0;
        for (var index = 0; index < sources.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = sources[index];
            var destination = ResolveDestinationPath(destinationFolder, Path.GetFileName(source));
            var partial = destination + ".winlive-part";
            try
            {
                await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan);
                await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await input.CopyToAsync(output, cancellationToken);
                }
                File.Move(partial, destination);
                File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(source));
                bytes += input.Length;
            }
            finally
            {
                if (File.Exists(partial)) File.Delete(partial);
            }
            progress?.Report((index + 1, sources.Length));
        }
        return new ExportResult(media.Length, sources.Length, bytes);
    }

    internal static string ResolveDestinationPath(string folder, string fileName)
    {
        var candidate = Path.Combine(folder, fileName);
        if (!File.Exists(candidate) && !File.Exists(candidate + ".winlive-part")) return candidate;
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var copy = 2; ; copy++)
        {
            candidate = Path.Combine(folder, $"{stem} ({copy}){extension}");
            if (!File.Exists(candidate) && !File.Exists(candidate + ".winlive-part")) return candidate;
        }
    }
}
