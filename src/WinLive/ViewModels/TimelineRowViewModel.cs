using Microsoft.UI.Xaml;

namespace WinLive.ViewModels;

/// <summary>
/// A fixed-height row in the library timeline. Keeping headers and media rows as
/// separate items lets the outer ListView virtualize without measuring a whole day.
/// </summary>
public sealed class TimelineRowViewModel
{
    private TimelineRowViewModel(string? title, IReadOnlyList<MediaTileViewModel> tiles)
    {
        Title = title;
        Tiles = tiles;
    }

    public string? Title { get; }
    public IReadOnlyList<MediaTileViewModel> Tiles { get; }
    public bool IsHeader => Title is not null;
    public Visibility HeaderVisibility => IsHeader ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TilesVisibility => IsHeader ? Visibility.Collapsed : Visibility.Visible;
    public double Height => IsHeader ? 38 : (Tiles.Count == 0 ? 115 : Tiles[0].TileHeight + 3);

    public static TimelineRowViewModel Header(string title) => new(title, []);
    public static TimelineRowViewModel Media(IReadOnlyList<MediaTileViewModel> tiles) => new(null, tiles);
}
