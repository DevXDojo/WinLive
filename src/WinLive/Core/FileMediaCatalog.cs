using System.Collections.Frozen;

namespace WinLive.Core;

public sealed class FileMediaCatalog(IMediaDecoder decoder) : IMediaCatalog
{
    private static readonly FrozenSet<string> PhotoExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".heic" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    private static readonly FrozenSet<string> VideoExtensions = new[] { ".mp4", ".mov", ".mkv" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<MediaGroup>> ScanAsync(string folderPath, CancellationToken cancellationToken) =>
        Task.Run(() => ScanCoreAsync(folderPath, cancellationToken), cancellationToken);

    private async Task<IReadOnlyList<MediaGroup>> ScanCoreAsync(string folderPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"The folder '{folderPath}' does not exist.");
        }

        var candidates = new List<FileInfo>();
        foreach (var path in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSupported(path))
            {
                candidates.Add(new FileInfo(path));
            }
        }

        var motionsByKey = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var motion in candidates.Where(IsMov))
        {
            motionsByKey.TryAdd(PairKey(motion), motion);
        }
        var heicKeys = candidates.Where(IsHeic).Select(PairKey).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        var items = new List<MediaItem>(candidates.Count);
        foreach (var file in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsMov(file) && heicKeys.Contains(PairKey(file)))
            {
                continue;
            }

            var motion = IsHeic(file) && motionsByKey.TryGetValue(PairKey(file), out var pairedMotion)
                ? pairedMotion
                : null;
            var kind = motion is not null ? MediaKind.LivePhoto : IsVideo(file) ? MediaKind.Video : MediaKind.Photo;
            var duration = kind is MediaKind.Video or MediaKind.LivePhoto
                ? await decoder.GetDurationAsync(motion?.FullName ?? file.FullName, cancellationToken)
                : null;
            var primaryRange = decoder.DetectDynamicRange(file.FullName);
            var range = primaryRange == DynamicRange.Standard && motion is not null
                ? decoder.DetectDynamicRange(motion.FullName)
                : primaryRange;
            items.Add(new MediaItem(
                Id: BuildId(file),
                Kind: kind,
                PrimaryPath: file.FullName,
                MotionPath: motion?.FullName,
                CapturedAt: file.LastWriteTimeUtc,
                Duration: duration,
                DynamicRange: range));
        }

        return items
            .OrderByDescending(item => item.CapturedAt)
            .GroupBy(item => item.CapturedAt.Date)
            .Select(group => new MediaGroup(group.Key == DateTimeOffset.UtcNow.Date ? "Today" : group.Key.ToString("D"), group.ToArray()))
            .ToArray();
    }

    internal static bool IsSupported(string path) => PhotoExtensions.Contains(Path.GetExtension(path)) || VideoExtensions.Contains(Path.GetExtension(path));
    private static bool IsHeic(FileInfo file) => Path.GetExtension(file.Name).Equals(".heic", StringComparison.OrdinalIgnoreCase);
    private static bool IsMov(FileInfo file) => Path.GetExtension(file.Name).Equals(".mov", StringComparison.OrdinalIgnoreCase);
    private static bool IsVideo(FileInfo file) => VideoExtensions.Contains(Path.GetExtension(file.Name));
    private static string PairKey(FileInfo file) => Path.Combine(file.DirectoryName ?? string.Empty, Path.GetFileNameWithoutExtension(file.Name));
    private static string BuildId(FileInfo file) => $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}";
}
