using System.Security.Cryptography;

namespace WinLive.Core;

public sealed record DuplicateGroup(long FileSize, string Fingerprint, IReadOnlyList<MediaItem> Items);

/// <summary>Finds byte-identical primary media without decoding or modifying it.</summary>
public sealed class MediaDuplicateFinder
{
    public Task<IReadOnlyList<DuplicateGroup>> FindAsync(
        IEnumerable<MediaItem> items,
        IProgress<(int Completed, int Total)>? progress,
        CancellationToken cancellationToken) => Task.Run(async () =>
    {
        var candidates = items
            .Select(item => (Item: item, Size: TryGetLength(item.PrimaryPath)))
            .Where(value => value.Size >= 0)
            .GroupBy(value => value.Size)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group)
            .ToArray();
        var hashed = new List<(MediaItem Item, long Size, string Hash)>(candidates.Length);
        for (var index = 0; index < candidates.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = new FileStream(candidates[index].Item.PrimaryPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            hashed.Add((candidates[index].Item, candidates[index].Size, Convert.ToHexString(hash)));
            progress?.Report((index + 1, candidates.Length));
        }
        return (IReadOnlyList<DuplicateGroup>)hashed
            .GroupBy(value => (value.Size, value.Hash))
            .Where(group => group.Count() > 1)
            .OrderByDescending(group => group.Key.Size * group.Count())
            .Select(group => new DuplicateGroup(group.Key.Size, group.Key.Hash, group.Select(value => value.Item).ToArray()))
            .ToArray();
    }, cancellationToken);

    private static long TryGetLength(string path)
    {
        try { return new FileInfo(path).Length; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return -1; }
    }
}
