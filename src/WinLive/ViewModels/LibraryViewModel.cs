using System.Collections.ObjectModel;
using WinLive.Core;

namespace WinLive.ViewModels;

public sealed class LibraryViewModel
{
    private readonly List<MediaTileViewModel> _flattened = [];
    private readonly List<MediaGroupViewModel> _groups = [];
    private int _columns = 6;
    private MediaKind? _kindFilter;
    private string _searchQuery = string.Empty;
    private TimelineGrouping _grouping = TimelineGrouping.Month;
    private bool _favoritesOnly;
    private IReadOnlySet<string>? _albumMembers;
    private MediaSortOrder _sortOrder = MediaSortOrder.NewestFirst;
    private double _tileWidth = 150;
    private double _tileHeight = 112;
    private string? _extensionFilter;
    private string? _tagFilter;
    private int _dateRangeDays;
    public ObservableCollection<TimelineRowViewModel> TimelineRows { get; } = [];
    public ObservableCollection<TimelineRowViewModel> NavigationGroups { get; } = [];
    public IReadOnlyList<MediaTileViewModel> Flattened => _flattened;
    public IReadOnlyList<MediaTileViewModel> VisibleItems { get; private set; } = [];
    public int VisibleCount { get; private set; }
    public IReadOnlyList<MediaTileViewModel> SelectedItems => _flattened.Where(tile => tile.IsSelected).ToArray();

    public int IndexOf(MediaTileViewModel tile) => _flattened.IndexOf(tile);

    public void SetGroups(IEnumerable<MediaGroup> groups)
    {
        _groups.Clear();
        _flattened.Clear();
        foreach (var group in groups)
        {
            var viewModel = new MediaGroupViewModel(group);
            _groups.Add(viewModel);
            _flattened.AddRange(viewModel.Items);
        }
        foreach (var tile in _flattened) tile.ApplyTileSize(_tileWidth, _tileHeight);
        RebuildTimelineRows();
    }

    public void SetColumnCount(int columns)
    {
        columns = Math.Max(1, columns);
        if (_columns == columns) return;
        _columns = columns;
        RebuildTimelineRows();
    }

    public void SetFilter(MediaKind? kindFilter, string? searchQuery = null)
    {
        _kindFilter = kindFilter;
        if (searchQuery is not null) _searchQuery = searchQuery.Trim();
        RebuildTimelineRows();
    }

    public void SetGrouping(TimelineGrouping grouping)
    {
        if (_grouping == grouping) return;
        _grouping = grouping;
        RebuildTimelineRows();
    }

    public void SetOrganization(bool favoritesOnly, IReadOnlySet<string>? albumMembers, MediaSortOrder sortOrder)
    {
        _favoritesOnly = favoritesOnly;
        _albumMembers = albumMembers;
        _sortOrder = sortOrder;
        RebuildTimelineRows();
    }

    public void RefreshMetadata() => RebuildTimelineRows();

    public void SetSecondaryFilter(string? extension, string? tag, int dateRangeDays)
    {
        _extensionFilter = string.IsNullOrWhiteSpace(extension) ? null : extension.TrimStart('.');
        _tagFilter = string.IsNullOrWhiteSpace(tag) ? null : tag;
        _dateRangeDays = Math.Max(0, dateRangeDays);
        RebuildTimelineRows();
    }

    public void SetSelectionMode(bool enabled)
    {
        foreach (var tile in _flattened) tile.SetSelectionMode(enabled);
    }

    public void SetTileSize(double width, double height)
    {
        _tileWidth = width;
        _tileHeight = height;
        foreach (var tile in _flattened) tile.ApplyTileSize(width, height);
        RebuildTimelineRows();
    }

    private void RebuildTimelineRows()
    {
        TimelineRows.Clear();
        NavigationGroups.Clear();
        VisibleCount = 0;
        var visibleItems = new List<MediaTileViewModel>();
        foreach (var group in BuildDisplayGroups())
        {
            var matching = Sort(group.Items
                .Where(tile => _kindFilter is null || tile.Item.Kind == _kindFilter)
                .Where(tile => string.IsNullOrWhiteSpace(_searchQuery)
                    || tile.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)
                    || tile.Note.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)
                    || tile.Tags.Any(tag => tag.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)))
                .Where(tile => !_favoritesOnly || tile.IsFavorite)
                .Where(tile => _albumMembers is null || _albumMembers.Contains(tile.Item.PrimaryPath))
                .Where(tile => _extensionFilter is null || Path.GetExtension(tile.Item.PrimaryPath).TrimStart('.').Equals(_extensionFilter, StringComparison.OrdinalIgnoreCase))
                .Where(tile => _tagFilter is null || tile.Tags.Contains(_tagFilter, StringComparer.CurrentCultureIgnoreCase))
                .Where(tile => _dateRangeDays == 0 || tile.Item.CapturedAt >= DateTimeOffset.Now.AddDays(-_dateRangeDays)))
                .ToArray();
            if (matching.Length == 0) continue;
            if (group.Title is not null)
            {
                var header = TimelineRowViewModel.Header(group.Title);
                TimelineRows.Add(header);
                NavigationGroups.Add(header);
            }
            VisibleCount += matching.Length;
            visibleItems.AddRange(matching);
            for (var index = 0; index < matching.Length; index += _columns)
            {
                TimelineRows.Add(TimelineRowViewModel.Media(matching.Skip(index).Take(_columns).ToArray()));
            }
        }
        VisibleItems = visibleItems;
    }

    private MediaTileViewModel[] Sort(IEnumerable<MediaTileViewModel> items) => _sortOrder switch
    {
        MediaSortOrder.OldestFirst => items.OrderBy(tile => tile.Item.CapturedAt).ToArray(),
        MediaSortOrder.NameAscending => items.OrderBy(tile => tile.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(),
        MediaSortOrder.NameDescending => items.OrderByDescending(tile => tile.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(),
        _ => items.OrderByDescending(tile => tile.Item.CapturedAt).ToArray(),
    };

    private IEnumerable<(string? Title, IReadOnlyList<MediaTileViewModel> Items)> BuildDisplayGroups()
    {
        return _grouping switch
        {
            TimelineGrouping.None => [(null, (IReadOnlyList<MediaTileViewModel>)_flattened)],
            TimelineGrouping.Month => _flattened
                .GroupBy(tile => new DateTime(tile.Item.CapturedAt.Year, tile.Item.CapturedAt.Month, 1))
                .Select(group => ((string?)group.Key.ToString("Y"), (IReadOnlyList<MediaTileViewModel>)group.ToArray())),
            TimelineGrouping.Year => _flattened
                .GroupBy(tile => tile.Item.CapturedAt.Year)
                .Select(group => ((string?)group.Key.ToString(), (IReadOnlyList<MediaTileViewModel>)group.ToArray())),
            _ => _flattened
                .GroupBy(tile => tile.Item.CapturedAt.LocalDateTime.Date)
                .Select(group => ((string?)group.Key.ToString("D"), (IReadOnlyList<MediaTileViewModel>)group.ToArray())),
        };
    }
}

public enum TimelineGrouping
{
    None,
    Day,
    Month,
    Year,
}

public enum MediaSortOrder
{
    NewestFirst,
    OldestFirst,
    NameAscending,
    NameDescending,
}
