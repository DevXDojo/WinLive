namespace WinLive.Core;

/// <summary>
/// Uses the packaged native decoder when present. The shell stays functional with
/// Windows Media Foundation while a distribution build supplies FFmpeg and libheif.
/// </summary>
public sealed class WindowsMediaDecoder : IMediaDecoder
{
    public bool CanDecode(string path) => FileMediaCatalog.IsSupported(path);

    public DynamicRange DetectDynamicRange(string path)
    {
        var name = Path.GetFileName(path);
        if (name.Contains("dolby", StringComparison.OrdinalIgnoreCase) || name.Contains("dv", StringComparison.OrdinalIgnoreCase)) return DynamicRange.DolbyVisionBaseLayer;
        if (name.Contains("hlg", StringComparison.OrdinalIgnoreCase)) return DynamicRange.Hlg;
        if (name.Contains("hdr10", StringComparison.OrdinalIgnoreCase)) return DynamicRange.Hdr10;
        if (name.Contains("pq", StringComparison.OrdinalIgnoreCase)) return DynamicRange.Pq;
        if (Path.GetExtension(path).Equals(".heic", StringComparison.OrdinalIgnoreCase)) return DynamicRange.HeifGainMap;
        return DynamicRange.Standard;
    }

    public Task<TimeSpan?> GetDurationAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Duration probing is supplied by FFmpeg in a packaged build. Returning null is
        // deliberate: a missing duration must never prevent a file from being browsed.
        return Task.FromResult<TimeSpan?>(null);
    }
}
