namespace WinLive.Core;

public interface IMediaCatalog
{
    Task<IReadOnlyList<MediaGroup>> ScanAsync(string folderPath, CancellationToken cancellationToken);
}
