namespace WinLive.Core;

public enum MediaKind
{
    Photo,
    Video,
    LivePhoto,
}

public enum DynamicRange
{
    Standard,
    Hlg,
    Pq,
    Hdr10,
    DolbyVisionBaseLayer,
    HeifGainMap,
}

public sealed record MediaItem(
    string Id,
    MediaKind Kind,
    string PrimaryPath,
    string? MotionPath,
    DateTimeOffset CapturedAt,
    TimeSpan? Duration,
    DynamicRange DynamicRange)
{
    public string DisplayName => Path.GetFileName(PrimaryPath);
    public bool HasMotion => MotionPath is not null;
}

public sealed record MediaGroup(string Title, IReadOnlyList<MediaItem> Items);
