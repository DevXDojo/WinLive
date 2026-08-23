namespace WinLive.Core;

/// <summary>Backend boundary for the packaged HEIF/FFmpeg decoder implementation.</summary>
public interface IMediaDecoder
{
    bool CanDecode(string path);
    DynamicRange DetectDynamicRange(string path);
    Task<TimeSpan?> GetDurationAsync(string path, CancellationToken cancellationToken);
}
