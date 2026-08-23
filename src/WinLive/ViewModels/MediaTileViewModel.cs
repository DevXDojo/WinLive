using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using System.ComponentModel;
using WinLive.Core;

namespace WinLive.ViewModels;

public sealed class MediaTileViewModel(MediaItem item) : INotifyPropertyChanged
{
    private ImageSource? _thumbnail;
    private Task? _thumbnailTask;
    private CancellationTokenSource? _thumbnailCancellation;
    private int _thumbnailGeneration;
    private bool _isFavorite;
    private double _tileWidth = 150;
    private double _tileHeight = 112;
    private bool _isSelected;
    private bool _isSelectionMode;
    private string _note = string.Empty;
    private IReadOnlyList<string> _tags = [];
    public MediaItem Item { get; } = item;
    public string Name => Item.DisplayName;
    public string Badge => Item.Kind == MediaKind.LivePhoto ? "LIVE" : Item.Kind == MediaKind.Video ? "VIDEO" : string.Empty;
    public string HdrBadge => Item.DynamicRange == DynamicRange.Standard ? string.Empty : "HDR";
    public Visibility BadgeVisibility => string.IsNullOrEmpty(Badge) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility HdrBadgeVisibility => string.IsNullOrEmpty(HdrBadge) ? Visibility.Collapsed : Visibility.Visible;
    public bool IsFavorite
    {
        get => _isFavorite;
        private set
        {
            if (_isFavorite == value) return;
            _isFavorite = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFavorite)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FavoriteVisibility)));
        }
    }
    public Visibility FavoriteVisibility => IsFavorite ? Visibility.Visible : Visibility.Collapsed;
    public double TileWidth => _tileWidth;
    public double TileHeight => _tileHeight;
    public string Extension => Path.GetExtension(Item.PrimaryPath).TrimStart('.').ToUpperInvariant();
    public string Note => _note;
    public IReadOnlyList<string> Tags => _tags;
    public string TagText => _tags.Count == 0 ? string.Empty : string.Join(" · ", _tags.Take(2));
    public Visibility TagVisibility => _tags.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    public bool IsSelected
    {
        get => _isSelected;
        private set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectionMark)));
        }
    }
    public string SelectionMark => IsSelected ? "✓" : string.Empty;
    public Visibility SelectionIndicatorVisibility => _isSelectionMode ? Visibility.Visible : Visibility.Collapsed;
    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        private set
        {
            if (ReferenceEquals(_thumbnail, value)) return;
            _thumbnail = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ApplyFavorite(bool isFavorite) => IsFavorite = isFavorite;

    public void ApplyAnnotation(MediaAnnotation annotation)
    {
        _note = annotation.Note;
        _tags = annotation.Tags;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Note)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tags)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TagText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TagVisibility)));
    }

    public void SetSelectionMode(bool enabled)
    {
        _isSelectionMode = enabled;
        if (!enabled) _isSelected = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectionMark)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectionIndicatorVisibility)));
    }

    public void ToggleSelected()
    {
        if (_isSelectionMode) IsSelected = !IsSelected;
    }

    public void ApplyTileSize(double width, double height)
    {
        if (_tileWidth == width && _tileHeight == height) return;
        _tileWidth = width;
        _tileHeight = height;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TileWidth)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TileHeight)));
    }

    public void RequestThumbnail()
    {
        if (_thumbnail is not null || _thumbnailTask is not null) return;
        var generation = ++_thumbnailGeneration;
        _thumbnailCancellation = new CancellationTokenSource();
        _thumbnailTask = LoadThumbnailAsync(generation, _thumbnailCancellation.Token);
    }

    public void ReleaseThumbnail()
    {
        ++_thumbnailGeneration;
        _thumbnailCancellation?.Cancel();
        _thumbnailCancellation?.Dispose();
        _thumbnailCancellation = null;
        _thumbnailTask = null;
        Thumbnail = null;
    }

    private async Task LoadThumbnailAsync(int generation, CancellationToken cancellationToken)
    {
        try
        {
            var thumbnail = await ThumbnailCache.GetAsync(Item, cancellationToken);
            if (generation == _thumbnailGeneration && !cancellationToken.IsCancellationRequested)
            {
                Thumbnail = thumbnail;
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (generation == _thumbnailGeneration)
            {
                _thumbnailTask = null;
                _thumbnailCancellation?.Dispose();
                _thumbnailCancellation = null;
            }
        }
    }
}
