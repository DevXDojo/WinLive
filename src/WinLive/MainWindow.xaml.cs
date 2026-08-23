using Microsoft.UI.Windowing;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Core;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using WinLive.Core;
using WinLive.ViewModels;

namespace WinLive;

public sealed partial class MainWindow : Window
{
    private readonly IMediaCatalog _catalog = new FileMediaCatalog(new WindowsMediaDecoder());
    private readonly MediaLibraryStateStore _libraryState = new();
    private readonly MediaExporter _exporter = new();
    private readonly DispatcherTimer _slideshowTimer = new();
    private readonly Random _random = new();
    private CancellationTokenSource? _scanCancellation;
    private CancellationTokenSource? _searchCancellation;
    private MediaKind? _currentFilter;
    private int _viewerIndex = -1;
    private bool _isMuted;
    private bool _isFullscreen;
    private double _zoom = 1;
    private double _rotation;
    private double _panX;
    private double _panY;
    private Windows.Foundation.Point _panStart;
    private Windows.Foundation.Point _panOrigin;
    private bool _isPanning;
    private int _presentationSession;
    private CancellationTokenSource? _gridLivePressCancellation;
    private MediaTileViewModel? _gridLivePressedTile;
    private bool _suppressTileClick;
    private BitmapImage? _preparedLiveStill;
    private bool _isInitialized;
    private string? _activeAlbum;
    private string? _currentFolderPath;
    private double _tileWidth = 150;
    private bool _isSelectionMode;
    private string? _activeSmartView;
    private bool _isApplyingPreferences;
    private IReadOnlyList<MediaTileViewModel>? _filmstripSource;
    private Windows.Foundation.TypedEventHandler<Windows.Media.Playback.MediaPlayer, object>? _pendingLiveMediaOpened;

    public MainWindow()
    {
        LocalizationService.Apply(ParseLanguage(UserSettings.LoadLanguage()));
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        SetTitleBar(TitleBarDragRegion);
        AppWindow.Title = "WinLive";
        var windowIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
        if (File.Exists(windowIconPath)) AppWindow.SetIcon(windowIconPath);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 820));
        ViewModel = new LibraryViewModel();
        Root.DataContext = ViewModel;
        ViewModel.NavigationGroups.CollectionChanged += (_, _) => UpdateTimeNavigationVisibility();
        ApplyPreferences();
        ApplyLocalizedText();
        _isMuted = UserSettings.LoadMuted();
        UpdateMuteUi();
        RefreshAlbumFilter();
        RefreshSmartViews();
        RefreshSecondaryFilters();
        RestoreSlideshowInterval();
        _slideshowTimer.Tick += SlideshowTimer_Tick;
        Root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(Root_KeyDown), true);
        Root.AddHandler(UIElement.KeyUpEvent, new KeyEventHandler(Root_KeyUp), true);
        Root.ActualThemeChanged += (_, _) => { UpdateTitleBarTheme(); UpdateNavigationVisuals(); };
        _isInitialized = true;
        UpdateNavigationVisuals();
        UpdateEmptyState();
    }

    public LibraryViewModel ViewModel { get; }
    public ObservableCollection<MediaTileViewModel> ViewerFilmstripItems { get; } = new BulkObservableCollection<MediaTileViewModel>();

    private static string L(string key) => LocalizationService.Get(key);
    private static string LF(string key, params object[] args) => LocalizationService.Format(key, args);

    private void ApplyPreferences()
    {
        _isApplyingPreferences = true;
        var language = ParseLanguage(UserSettings.LoadLanguage());
        LanguageBox.SelectedIndex = language switch { AppLanguageMode.English => 1, AppLanguageMode.SimplifiedChinese => 2, _ => 0 };
        ThemeBox.SelectedIndex = UserSettings.LoadTheme() switch { "Light" => 1, "Dark" => 2, _ => 0 };
        ApplyTheme(UserSettings.LoadTheme());
        _isApplyingPreferences = false;
    }

    private static AppLanguageMode ParseLanguage(string? value) => value switch
    {
        "English" => AppLanguageMode.English,
        "SimplifiedChinese" => AppLanguageMode.SimplifiedChinese,
        _ => AppLanguageMode.System,
    };

    private void ApplyTheme(string? value)
    {
        Root.RequestedTheme = value switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default };
        UpdateTitleBarTheme();
        UpdateNavigationVisuals();
    }

    private void UpdateTitleBarTheme()
    {
        var dark = Root.ActualTheme == ElementTheme.Dark;
        AppWindow.TitleBar.BackgroundColor = dark ? Windows.UI.Color.FromArgb(255, 32, 32, 32) : Windows.UI.Color.FromArgb(255, 243, 243, 243);
        AppWindow.TitleBar.ForegroundColor = dark ? Windows.UI.Color.FromArgb(255, 245, 245, 245) : Windows.UI.Color.FromArgb(255, 28, 28, 28);
        AppWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        AppWindow.TitleBar.ButtonForegroundColor = dark ? Windows.UI.Color.FromArgb(255, 245, 245, 245) : Windows.UI.Color.FromArgb(255, 28, 28, 28);
    }

    private void TitleBarMinimize_Click(object sender, RoutedEventArgs e)
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.Minimize();
    }

    private void TitleBarMaximize_Click(object sender, RoutedEventArgs e)
    {
        if (AppWindow.Presenter is not OverlappedPresenter presenter) return;
        if (presenter.State == OverlappedPresenterState.Maximized) presenter.Restore();
        else presenter.Maximize();
    }

    private void TitleBarClose_Click(object sender, RoutedEventArgs e) => Close();

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsGeneral_Click(sender, e);
        SettingsOverlay.Visibility = Visibility.Visible;
        ApplySupplementalLocalizedText();
        DispatcherQueue.TryEnqueue(ApplySupplementalLocalizedText);
    }

    private void CloseSettings_Click(object sender, RoutedEventArgs e) => SettingsOverlay.Visibility = Visibility.Collapsed;

    private void SettingsGeneral_Click(object sender, RoutedEventArgs e)
    {
        SettingsGeneralPanel.Visibility = Visibility.Visible;
        SettingsKeyboardPanel.Visibility = Visibility.Collapsed;
        SettingsAboutPanel.Visibility = Visibility.Collapsed;
        SettingsGeneralButton.IsChecked = true;
        SettingsKeyboardButton.IsChecked = false;
        SettingsAboutButton.IsChecked = false;
    }

    private void SettingsKeyboard_Click(object sender, RoutedEventArgs e)
    {
        SettingsGeneralPanel.Visibility = Visibility.Collapsed;
        SettingsKeyboardPanel.Visibility = Visibility.Visible;
        SettingsAboutPanel.Visibility = Visibility.Collapsed;
        SettingsGeneralButton.IsChecked = false;
        SettingsKeyboardButton.IsChecked = true;
        SettingsAboutButton.IsChecked = false;
    }

    private void SettingsAbout_Click(object sender, RoutedEventArgs e)
    {
        SettingsGeneralPanel.Visibility = Visibility.Collapsed;
        SettingsKeyboardPanel.Visibility = Visibility.Collapsed;
        SettingsAboutPanel.Visibility = Visibility.Visible;
        SettingsGeneralButton.IsChecked = false;
        SettingsKeyboardButton.IsChecked = false;
        SettingsAboutButton.IsChecked = true;
    }

    private void ToggleFilterPanel_Click(object sender, RoutedEventArgs e)
    {
        FilterOverlay.Visibility = FilterOverlay.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        if (FilterOverlay.Visibility == Visibility.Visible) ApplySupplementalLocalizedText();
        if (FilterOverlay.Visibility == Visibility.Visible) DispatcherQueue.TryEnqueue(ApplySupplementalLocalizedText);
    }

    private void CloseFilterPanel_Click(object sender, RoutedEventArgs e) => FilterOverlay.Visibility = Visibility.Collapsed;

    private void ApplyLocalizedText()
    {
        AllNavText.Text = L("AllMedia");
        if (string.IsNullOrWhiteSpace(_currentFolderPath)) FolderNameText.Text = L("NoFolder");
        SettingsTitleText.Text = L("Settings");
        SettingsSubtitleText.Text = L("WinLivePreferences");
        SettingsPreferencesText.Text = L("Preferences");
        SettingsGeneralText.Text = L("General"); SettingsKeyboardText.Text = L("Keyboard"); SettingsAboutText.Text = L("About");
        SettingsAppearanceTitleText.Text = L("AppearanceLanguage"); SettingsApplyImmediatelyText.Text = L("ApplyImmediately");
        SettingsLanguageText.Text = L("Language"); SettingsLanguageDescriptionText.Text = L("LanguageDescription");
        SettingsThemeText.Text = L("Theme"); SettingsThemeDescriptionText.Text = L("ThemeDescription");
        SettingsShortcutTitleText.Text = L("Shortcuts"); SettingsShortcutDescriptionText.Text = L("ShortcutDescription");
        SettingsOpenFolderShortcutText.Text = L("OpenFolder"); SettingsSearchShortcutText.Text = L("Search");
        SettingsThumbnailShortcutText.Text = L("ThumbnailShortcut"); SettingsShortcutReferenceText.Text = L("ShortcutReference");
        SettingsPreviousNextText.Text = L("PreviousNext"); SettingsPlayPauseText.Text = L("PlayPause");
        SettingsFavoriteText.Text = L("Favorite"); SettingsDetailsText.Text = L("Details"); SettingsZoomFitText.Text = L("ZoomFit");
        SettingsFullscreenText.Text = L("Fullscreen"); SettingsBackText.Text = L("Back");
        AboutDescriptionText.Text = L("AboutDescription");
        AboutVersionText.Text = $"{L("Version")} {typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}";
        AboutLocalFirstText.Text = L("LocalFirst"); AboutLocalFirstDescriptionText.Text = L("LocalFirstDescription");
        AboutGitHubText.Text = L("OpenSource"); AboutGitHubDescriptionText.Text = L("GitHubDescription"); AboutGitHubButtonText.Text = L("GitHubProject");
        LanguageSystemItem.Content = L("System"); LanguageEnglishItem.Content = L("English"); LanguageChineseItem.Content = L("Chinese"); ThemeSystemItem.Content = L("System"); ThemeLightItem.Content = L("Light"); ThemeDarkItem.Content = L("Dark");
        SearchBox.PlaceholderText = L("SearchPlaceholder"); OpenFolderText.Text = L("OpenFolder"); FilterButtonText.Text = L("Filter"); FilterTitleText.Text = L("FilterTitle"); ClearFiltersButton.Content = L("ClearFilters");
        SortNewestItem.Content = L("SortNewest"); SortOldestItem.Content = L("SortOldest"); SortNameAscendingItem.Content = L("SortNameAZ"); SortNameDescendingItem.Content = L("SortNameZA");
        DateAnyItem.Content = L("AnyDate"); Date7Item.Content = L("Last7"); Date30Item.Content = L("Last30"); Date365Item.Content = L("LastYear");
        ToolTipService.SetToolTip(CreateAlbumButton, L("NewAlbum")); ToolTipService.SetToolTip(DeleteAlbumButton, L("DeleteAlbum")); ToolTipService.SetToolTip(SaveViewButton, L("SaveView")); ToolTipService.SetToolTip(DeleteViewButton, L("DeleteView"));
        ToolTipService.SetToolTip(PhotoNavButton, L("Photos")); ToolTipService.SetToolTip(LiveNavButton, L("LivePhotos")); ToolTipService.SetToolTip(VideoNavButton, L("Videos")); ToolTipService.SetToolTip(FavoritesFilterButton, L("Favorites"));
        ToolTipService.SetToolTip(GroupNoneItem, L("GroupNone")); ToolTipService.SetToolTip(GroupDayItem, L("GroupDay")); ToolTipService.SetToolTip(GroupMonthItem, L("GroupMonth")); ToolTipService.SetToolTip(GroupYearItem, L("GroupYear"));
        ToolTipService.SetToolTip(DensitySmallItem, L("Small")); ToolTipService.SetToolTip(DensityMediumItem, L("Medium")); ToolTipService.SetToolTip(DensityLargeItem, L("Large")); ToolTipService.SetToolTip(InsightsButton, L("Insights"));
        EmptyStatePrimaryButton.Content = L("ChooseFolder"); EmptyStateClearButton.Content = L("ClearFilters");
        InsightsTitleText.Text = L("Insights"); InsightsSubtitleText.Text = L("InsightsSubtitle");
        InsightOverviewLabel.Text = L("Overview").ToUpperInvariant(); InsightTotalLabel.Text = L("TotalItems"); InsightFavoritesLabel.Text = L("Favorites"); InsightAlbumsLabel.Text = L("Albums");
        InsightBreakdownLabel.Text = L("MediaBreakdown").ToUpperInvariant(); InsightPhotosLabel.Text = L("Photos"); InsightLiveLabel.Text = L("LivePhotos"); InsightVideosLabel.Text = L("Videos"); InsightHdrLabel.Text = "HDR";
        InsightDateLabel.Text = L("DateRange").ToUpperInvariant(); InsightStorageLabel.Text = L("Storage").ToUpperInvariant(); InsightAlbumListLabel.Text = L("Albums").ToUpperInvariant();
        BatchFavoriteButton.Content = L("Favorite"); BatchAlbumButton.Content = L("AddAlbum"); ExportButton.Content = L("ExportCopies"); SelectVisibleButton.Content = L("SelectVisible"); DoneButton.Content = L("Done");
        DetailsTitleText.Text = L("Details"); AddToAlbumButton.Content = L("AddAlbum"); EditTagsButton.Content = L("EditTags"); SlideshowButton.Content = L("Slideshow"); CopyPathButton.Content = L("CopyPath"); RevealButton.Content = L("Reveal");
        SlideshowOptionsText.Text = L("SlideshowOptions"); SlideshowIntervalBox.Header = L("ChangeEvery"); Seconds2Item.Content = L("Seconds2"); Seconds3Item.Content = L("Seconds3"); Seconds5Item.Content = L("Seconds5"); Seconds10Item.Content = L("Seconds10"); ShuffleButton.Content = L("Shuffle"); LoopButton.Content = L("Loop");
        ToolTipService.SetToolTip(BackButton, L("Back")); ToolTipService.SetToolTip(PreviousButton, L("Previous")); ToolTipService.SetToolTip(NextButton, L("Next")); ToolTipService.SetToolTip(PlayPauseButton, L("PlayPause")); ToolTipService.SetToolTip(SettingsButton, L("Settings"));
        ToolTipService.SetToolTip(FavoriteButton, L("Favorite")); ToolTipService.SetToolTip(InfoButton, L("Details")); ToolTipService.SetToolTip(ViewerMoreButton, L("More"));
        ToolTipService.SetToolTip(FitButton, L("Fit")); ToolTipService.SetToolTip(MuteButton, _isMuted ? L("Unmute") : L("Mute"));
        ToolTipService.SetToolTip(SelectionModeButton, _isSelectionMode ? L("Cancel") : L("Select"));
        ApplySupplementalLocalizedText();
        var selectedTag = TagFilterBox.SelectedIndex > 0 ? TagFilterBox.SelectedItem as string : null;
        var selectedExtension = ExtensionFilterBox.SelectedIndex > 0 ? ExtensionFilterBox.SelectedItem as string : null;
        RefreshAlbumFilter(_activeAlbum); RefreshSmartViews(_activeSmartView); RefreshSecondaryFilters(selectedTag, selectedExtension);
        ViewModel.RefreshMetadata();
        UpdateLibraryTitle(); UpdateLibraryStatus(); UpdateMuteUi(); UpdateSelectionUi();
        if (_viewerIndex >= 0) ShowCurrentItem();
        if (InsightsPanel.Visibility == Visibility.Visible) UpdateInsights();
    }

    private static void LocalizeLiteral(DependencyObject root, string value, params string[] knownValues)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is TextBlock text && knownValues.Contains(text.Text, StringComparer.Ordinal)) text.Text = value;
            LocalizeLiteral(child, value, knownValues);
        }
    }

    private void ApplySupplementalLocalizedText()
    {
        LocalizeLiteral(Root, L("FilterSubtitle"), "Narrow this view", "缩小当前结果范围");
        LocalizeLiteral(Root, L("Collections"), "Collections", "集合");
        LocalizeLiteral(Root, L("Date"), "Date", "日期");
        LocalizeLiteral(Root, L("Metadata"), "Metadata", "元数据");
        LocalizeLiteral(Root, L("General"), "General", "常规");
        LocalizeLiteral(Root, L("Keyboard"), "Keyboard", "键盘");
        LocalizeLiteral(Root, L("Language"), "Language", "语言");
        LocalizeLiteral(Root, L("Theme"), "Theme", "主题");
        LocalizeLiteral(Root, L("AppearanceLanguage"), "Appearance and language", "外观与语言");
        LocalizeLiteral(Root, L("Shortcuts"), "Keyboard shortcuts", "键盘快捷键");
        LocalizeLiteral(Root, L("OpenFolder"), "Open folder", "打开文件夹");
        LocalizeLiteral(Root, L("Search"), "Search", "搜索");
        LocalizeLiteral(Root, L("PreviousNext"), "Previous / next", "上一项 / 下一项");
        LocalizeLiteral(Root, L("PlayPause"), "Play / pause", "播放 / 暂停");
        LocalizeLiteral(Root, L("Favorite"), "Favorite", "收藏");
        LocalizeLiteral(Root, L("Details"), "Details", "详细信息");
        LocalizeLiteral(Root, L("ZoomFit"), "Zoom / fit", "缩放 / 适应窗口");
        LocalizeLiteral(Root, L("Fullscreen"), "Full screen", "全屏");
        LocalizeLiteral(Root, L("Back"), "Back", "返回");
        LocalizeLiteral(Root, L("Organize"), "ORGANIZE", "整理");
        LocalizeLiteral(Root, L("View"), "VIEW", "查看");
        LocalizeLiteral(Root, L("File"), "FILE", "文件");
        LocalizeLiteral(Root, L("Preferences"), "PREFERENCES", "偏好设置");
        LocalizeLiteral(Root, L("WinLivePreferences"), "WinLive preferences", "WinLive 偏好设置");
        LocalizeLiteral(Root, L("ApplyImmediately"), "Changes apply immediately.", "更改会立即生效。");
        LocalizeLiteral(Root, L("LanguageDescription"), "Language used throughout WinLive", "WinLive 全局使用的界面语言");
        LocalizeLiteral(Root, L("ThemeDescription"), "Light, dark, or match Windows", "选择亮色、暗色或跟随 Windows");
        LocalizeLiteral(Root, L("ShortcutDescription"), "Fast access to common library and viewer actions.", "快速使用图库和查看器的常用操作。");
        LocalizeLiteral(Root, L("ThumbnailShortcut"), "Change thumbnail size", "更改缩略图大小");
        LocalizeLiteral(Root, L("ShortcutReference"), "Shortcut reference", "快捷键参考");
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingPreferences || LanguageBox.SelectedItem is not ComboBoxItem { Tag: string value }) return;
        var mode = ParseLanguage(value);
        UserSettings.SaveLanguage(value);
        LocalizationService.Apply(mode);
        ApplyLocalizedText();
    }

    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingPreferences || ThemeBox.SelectedItem is not ComboBoxItem { Tag: string value }) return;
        UserSettings.SaveTheme(value);
        ApplyTheme(value);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e) => _ = PickAndOpenFolderAsync();

    private async Task PickAndOpenFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            await LoadFolderAsync(folder.Path);
        }
    }

    private async Task LoadFolderAsync(string path)
    {
        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        _scanCancellation = new CancellationTokenSource();
        _currentFolderPath = path;
        FolderNameText.Text = Path.GetFileName(path);
        ToolTipService.SetToolTip(FolderNameText, path);
        StatusText.Text = L("Scanning");
        EmptyState.Visibility = Visibility.Visible;
        EmptyStateIcon.Glyph = "\uE895";
        EmptyStateTitle.Text = L("Scanning");
        EmptyStateDescription.Text = string.Empty;
        EmptyStatePrimaryButton.Visibility = Visibility.Collapsed;
        EmptyStateClearButton.Visibility = Visibility.Collapsed;
        ScanProgress.Visibility = Visibility.Visible;
        ScanProgress.IsActive = true;
        try
        {
            var groups = await _catalog.ScanAsync(path, _scanCancellation.Token);
            ThumbnailCache.Clear();
            ViewModel.SetGroups(groups);
            ApplyStoredMetadata();
            RefreshSecondaryFilters();
            UserSettings.SaveLastFolder(path);
            UpdateLibraryStatus();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = L("ScanCancelled");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            StatusText.Text = exception.Message;
        }
        finally
        {
            ScanProgress.IsActive = false;
            ScanProgress.Visibility = Visibility.Collapsed;
            UpdateEmptyState();
        }
    }

    private void MediaTile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MediaTileViewModel tile })
        {
            if (_isSelectionMode)
            {
                tile.ToggleSelected();
                UpdateSelectionUi();
                return;
            }
            if (_suppressTileClick && ReferenceEquals(tile, _gridLivePressedTile))
            {
                _suppressTileClick = false;
                _gridLivePressedTile = null;
                return;
            }
            StopGridLiveMotion();
            OpenViewer(ViewModel.IndexOf(tile));
        }
    }

    private void OpenViewer(int index)
    {
        if (index < 0 || index >= ViewModel.Flattened.Count)
        {
            return;
        }

        _viewerIndex = index;
        ViewerPanel.Visibility = Visibility.Visible;
        LibraryTitleBarContent.Visibility = Visibility.Collapsed;
        ViewerTitleBarContent.Visibility = Visibility.Visible;
        ViewerTopTools.Visibility = Visibility.Visible;
        ApplySupplementalLocalizedText();
        DispatcherQueue.TryEnqueue(ApplySupplementalLocalizedText);
        ViewerCanvas.Focus(FocusState.Programmatic);
        ShowCurrentItem();
    }

    private void ShowCurrentItem()
    {
        var session = ++_presentationSession;
        StopGridLiveMotion();
        ViewerPreloadImage.Source = null;
        _preparedLiveStill = null;
        StopMotion();
        var tile = ViewModel.Flattened[_viewerIndex];
        var item = tile.Item;
        ViewerTitle.Text = tile.Name;
        ViewerInfo.Text = $"{item.Kind} · {item.DynamicRange}";
        var visibleIndex = ViewModel.VisibleItems.ToList().IndexOf(tile);
        ViewerPosition.Text = LF("ViewerCount", visibleIndex >= 0 ? visibleIndex + 1 : _viewerIndex + 1, visibleIndex >= 0 ? ViewModel.VisibleItems.Count : ViewModel.Flattened.Count);
        var duration = item.Duration is { } value ? $"\n{L("Duration")}: {value:mm\\:ss}" : string.Empty;
        var size = TryGetMediaSize(item);
        var tags = tile.Tags.Count == 0 ? L("None") : string.Join(", ", tile.Tags);
        var note = string.IsNullOrWhiteSpace(tile.Note) ? L("None") : tile.Note;
        ViewerDetails.Text = $"{item.DisplayName}\n{item.Kind} · {item.DynamicRange}\n{L("Captured")}: {item.CapturedAt.LocalDateTime:G}{duration}\n{L("Size")}: {FormatBytes(size)}\n{L("Tags")}: {tags}\n{L("Note")}: {note}\n\n{item.PrimaryPath}";
        FavoriteButton.Content = tile.IsFavorite ? "♥" : "♡";
        UpdateFilmstrip(tile);
        ViewerInfoPanel.Visibility = Visibility.Collapsed;
        ViewerImage.Visibility = Visibility.Visible;
        ViewerPlayer.Visibility = Visibility.Collapsed;
        _zoom = 1;
        _rotation = 0;
        _panX = 0;
        _panY = 0;
        _isPanning = false;
        ApplyZoom();
        if (item.Kind == MediaKind.Video)
        {
            StartVideo(item.PrimaryPath, autoplay: true);
            return;
        }

        if (item.Kind == MediaKind.LivePhoto && item.MotionPath is { } motionPath)
        {
            _preparedLiveStill = CreateBitmap(item.PrimaryPath);
            // Keep the decoded original still directly beneath the video layer. When
            // the video ends, hiding its layer reveals this image without exposing the
            // player's transient clear-color frame.
            ViewerImage.Source = _preparedLiveStill;
            ViewerImage.Visibility = Visibility.Visible;
            ViewerPreloadImage.Source = _preparedLiveStill;
            _ = PlayLivePreviewAsync(motionPath, item.PrimaryPath, session);
            return;
        }

        // The viewer always renders the original still, never the gallery proxy.
        ViewerImage.Source = CreateImage(item.PrimaryPath);
    }

    private void UpdateFilmstrip(MediaTileViewModel current)
    {
        var visible = ViewModel.VisibleItems;
        var currentIndex = visible.ToList().IndexOf(current);
        if (currentIndex < 0) return;

        if (!ReferenceEquals(_filmstripSource, visible))
        {
            _filmstripSource = visible;
            ((BulkObservableCollection<MediaTileViewModel>)ViewerFilmstripItems).ReplaceAll(visible);
        }

        for (var index = Math.Max(0, currentIndex - 8); index < Math.Min(visible.Count, currentIndex + 9); index++)
            visible[index].RequestThumbnail();

        FilmstripList.SelectedItem = current;
        DispatcherQueue.TryEnqueue(() => CenterFilmstripOn(currentIndex));
    }

    private void CenterFilmstripOn(int itemIndex)
    {
        if (FindDescendant<ScrollViewer>(FilmstripList) is not { } scrollViewer) return;
        const double itemExtent = 72;
        var targetOffset = Math.Max(0, itemIndex * itemExtent - (scrollViewer.ViewportWidth - itemExtent) / 2);
        scrollViewer.ChangeView(targetOffset, null, null, false);
    }

    private void FilmstripList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.Item is MediaTileViewModel tile) tile.RequestThumbnail();
    }

    private void FilmstripList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is MediaTileViewModel tile) OpenViewer(ViewModel.IndexOf(tile));
    }

    private void FilmstripList_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(FilmstripList).Properties.MouseWheelDelta;
        if (delta == 0 || FindDescendant<ScrollViewer>(FilmstripList) is not { } scrollViewer) return;
        scrollViewer.ChangeView(scrollViewer.HorizontalOffset - delta * 1.35, null, null, false);
        e.Handled = true;
    }

    private static T? FindDescendant<T>(DependencyObject root, string? name = null) where T : FrameworkElement
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match && (name is null || match.Name == name)) return match;
            if (FindDescendant<T>(child, name) is { } nested) return nested;
        }
        return null;
    }

    private void MediaTile_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is DependencyObject root && FindDescendant<Border>(root, "TileCaption") is { } caption) caption.Opacity = 1;
    }

    private void MediaTile_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is DependencyObject root && FindDescendant<Border>(root, "TileCaption") is { } caption) caption.Opacity = 0;
    }

    private void MediaTile_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not Button { Tag: MediaTileViewModel tile } button) return;
        var menu = CreateItemContextMenu(tile, openItem: true);
        menu.ShowAt(button, e.GetPosition(button));
        e.Handled = true;
    }

    private void ViewerCanvas_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (_viewerIndex < 0) return;
        var menu = CreateItemContextMenu(ViewModel.Flattened[_viewerIndex], openItem: false);
        menu.ShowAt(ViewerCanvas, e.GetPosition(ViewerCanvas));
        e.Handled = true;
    }

    private MenuFlyout CreateItemContextMenu(MediaTileViewModel tile, bool openItem)
    {
        var menu = new MenuFlyout();
        if (openItem)
        {
            var open = new MenuFlyoutItem { Text = L("Open") };
            open.Click += (_, _) => OpenViewer(ViewModel.IndexOf(tile));
            menu.Items.Add(open);
            menu.Items.Add(new MenuFlyoutSeparator());
        }
        var favorite = new MenuFlyoutItem { Text = tile.IsFavorite ? L("Unfavorite") : L("Favorite") };
        favorite.Click += (_, _) => ToggleFavorite(tile);
        menu.Items.Add(favorite);
        menu.Items.Add(new MenuFlyoutSeparator());
        var copy = new MenuFlyoutItem { Text = L("CopyPath") };
        copy.Click += (_, _) => CopyPath(tile.Item.PrimaryPath);
        var reveal = new MenuFlyoutItem { Text = L("Reveal") };
        reveal.Click += (_, _) => RevealPath(tile.Item.PrimaryPath);
        menu.Items.Add(copy);
        menu.Items.Add(reveal);
        return menu;
    }

    private void ToggleFavorite(MediaTileViewModel tile)
    {
        var isFavorite = _libraryState.ToggleFavorite(tile.Item.PrimaryPath);
        tile.ApplyFavorite(isFavorite);
        ViewModel.RefreshMetadata();
        if (_viewerIndex >= 0 && ReferenceEquals(ViewModel.Flattened[_viewerIndex], tile)) FavoriteButton.Content = isFavorite ? "♥" : "♡";
        UpdateLibraryStatus();
    }

    private static void CopyPath(string path)
    {
        var package = new DataPackage();
        package.SetText(path);
        Clipboard.SetContent(package);
    }

    private static void RevealPath(string path)
    {
        if (File.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    private static ImageSource? CreateImage(string path)
    {
        return CreateBitmap(path);
    }

    private static BitmapImage? CreateBitmap(string path)
    {
        try { return new BitmapImage(new Uri(path)); }
        catch (UriFormatException) { return null; }
    }

    private async Task PlayLivePreviewAsync(string motionPath, string stillPath, int session)
    {
        if (session != _presentationSession || ViewerPanel.Visibility != Visibility.Visible) return;
        StartLiveVideo(motionPath, muted: true, session);
        await Task.Delay(800);
        if (session != _presentationSession) return;
        StopMotion();
        ViewerImage.Source = _preparedLiveStill ?? CreateImage(stillPath);
        ViewerPreloadImage.Source = null;
        _preparedLiveStill = null;
        ViewerImage.Visibility = Visibility.Visible;
    }

    private void StartVideo(string path, bool autoplay, bool? muted = null)
    {
        CancelPendingLiveReveal();
        ViewerPlayer.Opacity = 1;
        ViewerPlayer.Source = MediaSource.CreateFromUri(new Uri(path));
        ViewerPlayer.MediaPlayer.IsMuted = muted ?? _isMuted;
        ViewerPlayer.Visibility = Visibility.Visible;
        ViewerImage.Visibility = Visibility.Collapsed;
        if (autoplay)
        {
            ViewerPlayer.MediaPlayer.Play();
        }
    }

    private void StartLiveVideo(string path, bool muted, int session)
    {
        CancelPendingLiveReveal();
        ViewerImage.Visibility = Visibility.Visible;
        ViewerPlayer.Opacity = 0;
        ViewerPlayer.Visibility = Visibility.Visible;

        _pendingLiveMediaOpened = (_, _) =>
        {
            CancelPendingLiveReveal();
            DispatcherQueue.TryEnqueue(async () =>
            {
                // MediaOpened can precede the first composited frame. Keeping the
                // still visible for two frames prevents the player's clear color
                // from flashing through during the hand-off.
                await Task.Delay(50);
                if (session == _presentationSession && ViewerPanel.Visibility == Visibility.Visible && ViewerPlayer.Source is not null)
                    ViewerPlayer.Opacity = 1;
            });
        };
        ViewerPlayer.MediaPlayer.MediaOpened += _pendingLiveMediaOpened;
        ViewerPlayer.Source = MediaSource.CreateFromUri(new Uri(path));
        ViewerPlayer.MediaPlayer.IsMuted = muted;
        ViewerPlayer.MediaPlayer.Play();
    }

    private void StartLiveMotion()
    {
        if (_viewerIndex < 0 || ViewModel.Flattened[_viewerIndex].Item is not { Kind: MediaKind.LivePhoto, MotionPath: { } path })
        {
            return;
        }

        var session = ++_presentationSession;
        StartLiveVideo(path, _isMuted, session);
    }

    private void StopMotion()
    {
        CancelPendingLiveReveal();
        ViewerPlayer.Opacity = 0;
        ViewerPlayer.MediaPlayer?.Pause();
        ViewerPlayer.Source = null;
        ViewerPlayer.Visibility = Visibility.Collapsed;
    }

    private void CancelPendingLiveReveal()
    {
        if (_pendingLiveMediaOpened is null) return;
        ViewerPlayer.MediaPlayer.MediaOpened -= _pendingLiveMediaOpened;
        _pendingLiveMediaOpened = null;
    }

    private void ViewerCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(ViewerCanvas);
        if (!point.Properties.IsLeftButtonPressed) return;
        if (Math.Abs(_zoom - 1) < 0.001)
        {
            StartLiveMotion();
            return;
        }
        _isPanning = true;
        _panStart = point.Position;
        _panOrigin = new Windows.Foundation.Point(_panX, _panY);
        ViewerCanvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void ViewerCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPanning) return;
        var position = e.GetCurrentPoint(ViewerCanvas).Position;
        _panX = _panOrigin.X + position.X - _panStart.X;
        _panY = _panOrigin.Y + position.Y - _panStart.Y;
        ApplyZoom();
        e.Handled = true;
    }

    private void ViewerCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(ViewerCanvas).Properties.MouseWheelDelta;
        if (delta == 0) return;
        _zoom = Math.Clamp(_zoom * (delta > 0 ? 1.12 : 1 / 1.12), 0.25, 4);
        ApplyZoom();
        e.Handled = true;
    }

    private void ViewerCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ViewerCanvas.Clip = new RectangleGeometry
        {
            Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height)
        };
    }

    private void ViewerCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            if (e is not null) ViewerCanvas.ReleasePointerCapture(e.Pointer);
            return;
        }
        if (_viewerIndex >= 0 && ViewModel.Flattened[_viewerIndex].Item.Kind == MediaKind.LivePhoto)
        {
            ++_presentationSession;
            StopMotion();
            ViewerImage.Visibility = Visibility.Visible;
        }
    }

    private void CloseViewer_Click(object sender, RoutedEventArgs e) => CloseViewer();

    private void CloseViewer()
    {
        StopSlideshow();
        ++_presentationSession;
        ViewerPreloadImage.Source = null;
        _preparedLiveStill = null;
        StopMotion();
        ViewerPanel.Visibility = Visibility.Collapsed;
        ViewerTitleBarContent.Visibility = Visibility.Collapsed;
        ViewerTopTools.Visibility = Visibility.Collapsed;
        LibraryTitleBarContent.Visibility = Visibility.Visible;
        _viewerIndex = -1;
    }

    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var controlDown = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        if (controlDown && e.Key == Windows.System.VirtualKey.O)
        {
            _ = PickAndOpenFolderAsync();
            e.Handled = true;
            return;
        }
        if (controlDown && e.Key == Windows.System.VirtualKey.F)
        {
            SearchBox.Focus(FocusState.Programmatic);
            e.Handled = true;
            return;
        }
        if (e.Key == Windows.System.VirtualKey.F1)
        {
            SettingsOverlay.Visibility = Visibility.Visible;
            SettingsKeyboard_Click(SettingsKeyboardButton, e);
            ApplySupplementalLocalizedText();
            DispatcherQueue.TryEnqueue(ApplySupplementalLocalizedText);
            e.Handled = true;
            return;
        }
        if (e.Key == Windows.System.VirtualKey.F5 && !string.IsNullOrWhiteSpace(_currentFolderPath))
        {
            _ = LoadFolderAsync(_currentFolderPath);
            e.Handled = true;
            return;
        }
        if (e.Key == Windows.System.VirtualKey.M)
        {
            ToggleMute();
            e.Handled = true;
            return;
        }
        if (ViewerPanel.Visibility == Visibility.Visible && SettingsOverlay.Visibility != Visibility.Visible)
        {
            HandleViewerKeyDown(e);
        }
    }

    private void Root_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Space && _viewerIndex >= 0 && ViewModel.Flattened[_viewerIndex].Item.Kind == MediaKind.LivePhoto)
        {
            ViewerCanvas_PointerReleased(ViewerCanvas, null!);
            e.Handled = true;
        }
    }

    private void HandleViewerKeyDown(KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Left: Navigate(-1); break;
            case Windows.System.VirtualKey.Right: Navigate(1); break;
            case Windows.System.VirtualKey.Home: OpenVisibleBoundary(first: true); break;
            case Windows.System.VirtualKey.End: OpenVisibleBoundary(first: false); break;
            case Windows.System.VirtualKey.Escape:
                if (_isFullscreen) ToggleFullscreen(); else CloseViewer();
                break;
            case Windows.System.VirtualKey.Space: TogglePlaybackOrLive(); break;
            case Windows.System.VirtualKey.Number0: Fit(); break;
            case Windows.System.VirtualKey.Add: ZoomIn(); break;
            case Windows.System.VirtualKey.Subtract: ZoomOut(); break;
            case Windows.System.VirtualKey.F11: ToggleFullscreen(); break;
            case Windows.System.VirtualKey.I: ToggleInfo(); break;
            case Windows.System.VirtualKey.R: RotateRight(); break;
            case Windows.System.VirtualKey.F: ToggleCurrentFavorite(); break;
            case Windows.System.VirtualKey.S: ToggleSlideshow(); break;
            default: return;
        }
        e.Handled = true;
    }

    private void Navigate(int delta)
    {
        var visible = ViewModel.VisibleItems;
        if (visible.Count == 0) return;
        var current = _viewerIndex < 0 ? -1 : visible.ToList().IndexOf(ViewModel.Flattened[_viewerIndex]);
        current = current < 0 ? (delta > 0 ? -1 : visible.Count) : current;
        OpenViewer(ViewModel.IndexOf(visible[Math.Clamp(current + delta, 0, visible.Count - 1)]));
    }

    private void OpenVisibleBoundary(bool first)
    {
        if (ViewModel.VisibleItems.Count == 0) return;
        OpenViewer(ViewModel.IndexOf(first ? ViewModel.VisibleItems[0] : ViewModel.VisibleItems[^1]));
    }

    private void TimelineGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ViewModel.SetColumnCount(Math.Max(1, (int)(e.NewSize.Width / (_tileWidth + 3))));
    }

    private void TimelineGrid_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var controlDown = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        if (!controlDown) return;
        var delta = e.GetCurrentPoint(TimelineGrid).Properties.MouseWheelDelta;
        if (delta > 0)
        {
            if (_tileWidth < 130) SetGridDensity(150, 112);
            else SetGridDensity(194, 146);
        }
        else if (delta < 0)
        {
            if (_tileWidth >= 180) SetGridDensity(150, 112);
            else SetGridDensity(118, 88);
        }
        e.Handled = true;
    }

    private void TimeNavigation_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TimelineRowViewModel row) TimelineGrid.ScrollIntoView(row, ScrollIntoViewAlignment.Leading);
    }

    private void UpdateTimeNavigationVisibility()
        => TimeNavigation.Visibility = ViewModel.NavigationGroups.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

    private void AllFilter_Click(object sender, RoutedEventArgs e) => ApplyLibraryFilter(null);
    private void LiveFilter_Click(object sender, RoutedEventArgs e) => ApplyLibraryFilter(MediaKind.LivePhoto);
    private void VideoFilter_Click(object sender, RoutedEventArgs e) => ApplyLibraryFilter(MediaKind.Video);
    private void PhotoFilter_Click(object sender, RoutedEventArgs e) => ApplyLibraryFilter(MediaKind.Photo);
    private void GroupNone_Click(object sender, RoutedEventArgs e) => SetTimelineGrouping(TimelineGrouping.None);
    private void GroupDay_Click(object sender, RoutedEventArgs e) => SetTimelineGrouping(TimelineGrouping.Day);
    private void GroupMonth_Click(object sender, RoutedEventArgs e) => SetTimelineGrouping(TimelineGrouping.Month);
    private void GroupYear_Click(object sender, RoutedEventArgs e) => SetTimelineGrouping(TimelineGrouping.Year);

    private void SetTimelineGrouping(TimelineGrouping grouping)
    {
        ViewModel.SetGrouping(grouping);
        GroupNoneItem.IsChecked = grouping == TimelineGrouping.None;
        GroupDayItem.IsChecked = grouping == TimelineGrouping.Day;
        GroupMonthItem.IsChecked = grouping == TimelineGrouping.Month;
        GroupYearItem.IsChecked = grouping == TimelineGrouping.Year;
        UpdateLibraryStatus();
    }

    private void ApplyLibraryFilter(MediaKind? kind)
    {
        _currentFilter = kind;
        _activeSmartView = null;
        UpdateLibraryTitle();
        UpdateNavigationVisuals();
        ViewModel.SetFilter(kind, SearchBox.Text);
        UpdateLibraryStatus();
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();
        var cancellationToken = _searchCancellation.Token;
        try
        {
            await Task.Delay(120, cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;
            ViewModel.SetFilter(_currentFilter, SearchBox.Text);
            UpdateLibraryStatus();
        }
        catch (OperationCanceledException) { }
    }

    private void UpdateLibraryStatus()
    {
        if (ViewModel.Flattened.Count == 0)
        {
            StatusText.Text = string.IsNullOrWhiteSpace(_currentFolderPath) ? string.Empty : L("NoSupportedMedia");
            UpdateEmptyState();
            return;
        }
        var count = ViewModel.VisibleCount;
        StatusText.Text = count == ViewModel.Flattened.Count ? LF("Items", count) : LF("FilteredItems", count, ViewModel.Flattened.Count);
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        if (string.IsNullOrWhiteSpace(_currentFolderPath))
        {
            EmptyState.Visibility = Visibility.Visible;
            EmptyStateIcon.Glyph = "\uE8B7";
            EmptyStateTitle.Text = L("EmptyTitle");
            EmptyStateDescription.Text = L("EmptyDescription");
            EmptyStatePrimaryButton.Content = L("ChooseFolder");
            EmptyStatePrimaryButton.Visibility = Visibility.Visible;
            EmptyStateClearButton.Visibility = Visibility.Collapsed;
            return;
        }
        if (ViewModel.Flattened.Count == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            EmptyStateIcon.Glyph = "\uEB9F";
            EmptyStateTitle.Text = L("NoMediaTitle");
            EmptyStateDescription.Text = L("NoMediaDescription");
            EmptyStatePrimaryButton.Content = L("ChooseAnotherFolder");
            EmptyStatePrimaryButton.Visibility = Visibility.Visible;
            EmptyStateClearButton.Visibility = Visibility.Collapsed;
            return;
        }
        if (ViewModel.VisibleCount == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            EmptyStateIcon.Glyph = "\uE71C";
            EmptyStateTitle.Text = L("NoMatchesTitle");
            EmptyStateDescription.Text = L("NoMatchesDescription");
            EmptyStatePrimaryButton.Visibility = Visibility.Collapsed;
            EmptyStateClearButton.Visibility = Visibility.Visible;
            return;
        }
        EmptyState.Visibility = Visibility.Collapsed;
    }

    private void UpdateLibraryTitle()
    {
        if (!string.IsNullOrWhiteSpace(_activeSmartView))
        {
            LibraryTitleText.Text = _activeSmartView;
            return;
        }
        if (!string.IsNullOrWhiteSpace(_activeAlbum))
        {
            LibraryTitleText.Text = _activeAlbum;
            return;
        }
        var favorites = FavoritesFilterButton.IsChecked == true;
        var kindTitle = _currentFilter switch { MediaKind.LivePhoto => L("LivePhotos"), MediaKind.Video => L("Videos"), MediaKind.Photo => L("Photos"), _ => L("AllMedia") };
        LibraryTitleText.Text = favorites && _currentFilter is not null ? $"{L("Favorites")} · {kindTitle}" : favorites ? L("Favorites") : kindTitle;
    }

    private void UpdateNavigationVisuals()
    {
        AllNavButton.IsChecked = _currentFilter is null;
        LiveNavButton.IsChecked = _currentFilter == MediaKind.LivePhoto;
        VideoNavButton.IsChecked = _currentFilter == MediaKind.Video;
        PhotoNavButton.IsChecked = _currentFilter == MediaKind.Photo;
    }

    private void TimelineGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is not TimelineRowViewModel row || row.IsHeader) return;
        foreach (var tile in row.Tiles)
        {
            if (args.InRecycleQueue) tile.ReleaseThumbnail();
            else tile.RequestThumbnail();
        }
    }

    private void MediaTile_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_isSelectionMode) return;
        if (!e.GetCurrentPoint((UIElement)sender).Properties.IsLeftButtonPressed) return;
        if (sender is not Button { Tag: MediaTileViewModel { Item: { Kind: MediaKind.LivePhoto, MotionPath: not null } } tile }) return;
        _gridLivePressCancellation?.Cancel();
        _gridLivePressCancellation?.Dispose();
        _gridLivePressCancellation = new CancellationTokenSource();
        _gridLivePressedTile = tile;
        _ = StartGridLiveMotionAfterHoldAsync((FrameworkElement)sender, tile, _gridLivePressCancellation.Token);
        ((UIElement)sender).CapturePointer(e.Pointer);
    }

    private async Task StartGridLiveMotionAfterHoldAsync(FrameworkElement element, MediaTileViewModel tile, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(180, cancellationToken);
            if (cancellationToken.IsCancellationRequested || !ReferenceEquals(tile, _gridLivePressedTile) || tile.Item.MotionPath is not { } motionPath) return;
            var point = element.TransformToVisual(Root).TransformPoint(new Windows.Foundation.Point(0, 0));
            Canvas.SetLeft(GalleryLivePlayer, point.X);
            Canvas.SetTop(GalleryLivePlayer, point.Y);
            GalleryLivePlayer.Width = element.ActualWidth;
            GalleryLivePlayer.Height = element.ActualHeight;
            GalleryLivePlayer.Source = MediaSource.CreateFromUri(new Uri(motionPath));
            GalleryLivePlayer.MediaPlayer.IsMuted = _isMuted;
            GalleryLivePlayer.Visibility = Visibility.Visible;
            GalleryLivePlayer.MediaPlayer.Play();
            _suppressTileClick = true;
        }
        catch (OperationCanceledException) { }
    }

    private void MediaTile_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is UIElement element) element.ReleasePointerCapture(e.Pointer);
        StopGridLiveMotion();
    }

    private void MediaTile_LostPointerCapture(object sender, PointerRoutedEventArgs e) => StopGridLiveMotion();

    private void StopGridLiveMotion()
    {
        _gridLivePressCancellation?.Cancel();
        _gridLivePressCancellation?.Dispose();
        _gridLivePressCancellation = null;
        GalleryLivePlayer.MediaPlayer?.Pause();
        GalleryLivePlayer.Source = null;
        GalleryLivePlayer.Visibility = Visibility.Collapsed;
    }

    private void TogglePlaybackOrLive()
    {
        var item = ViewModel.Flattened[_viewerIndex].Item;
        if (item.Kind == MediaKind.LivePhoto) { StartLiveMotion(); return; }
        if (item.Kind == MediaKind.Video)
        {
            if (ViewerPlayer.MediaPlayer.PlaybackSession.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing) ViewerPlayer.MediaPlayer.Pause();
            else ViewerPlayer.MediaPlayer.Play();
        }
    }

    private void ToggleMute()
    {
        _isMuted = !_isMuted;
        UserSettings.SaveMuted(_isMuted);
        ViewerPlayer.MediaPlayer.IsMuted = _isMuted;
        GalleryLivePlayer.MediaPlayer.IsMuted = _isMuted;
        UpdateMuteUi();
    }

    private void Mute_Click(object sender, RoutedEventArgs e) => ToggleMute();
    private void Previous_Click(object sender, RoutedEventArgs e) => Navigate(-1);
    private void Next_Click(object sender, RoutedEventArgs e) => Navigate(1);
    private void Info_Click(object sender, RoutedEventArgs e) => ToggleInfo();
    private void PlayPause_Click(object sender, RoutedEventArgs e) => TogglePlaybackOrLive();
    private void ToggleInfo() => ViewerInfoPanel.Visibility = ViewerInfoPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    private void RevealInFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_viewerIndex < 0) return;
        var path = ViewModel.Flattened[_viewerIndex].Item.PrimaryPath;
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }
    private void UpdateMuteUi()
    {
        MuteButton.Content = new FontIcon { Glyph = _isMuted ? "\uE74F" : "\uE767", FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 12 };
        ToolTipService.SetToolTip(MuteButton, _isMuted ? L("Unmute") : L("Mute"));
    }
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomIn();
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomOut();
    private void Fit_Click(object sender, RoutedEventArgs e) => Fit();
    private void RotateLeft_Click(object sender, RoutedEventArgs e) => RotateLeft();
    private void RotateRight_Click(object sender, RoutedEventArgs e) => RotateRight();
    private void ZoomIn() { _zoom = Math.Min(4, _zoom + 0.25); ApplyZoom(); }
    private void ZoomOut() { _zoom = Math.Max(0.25, _zoom - 0.25); ApplyZoom(); }
    private void Fit() { _zoom = 1; _panX = 0; _panY = 0; ApplyZoom(); }
    private void RotateLeft() { _rotation = (_rotation - 90) % 360; ApplyZoom(); }
    private void RotateRight() { _rotation = (_rotation + 90) % 360; ApplyZoom(); }
    private void ApplyZoom()
    {
        ViewerMediaLayer.RenderTransform = new CompositeTransform
        {
            ScaleX = _zoom,
            ScaleY = _zoom,
            Rotation = _rotation,
            TranslateX = _panX,
            TranslateY = _panY,
        };
        ZoomText.Text = $"{_zoom:P0}";
    }
    private void ToggleFullscreen()
    {
        _isFullscreen = !_isFullscreen;
        AppWindow.SetPresenter(_isFullscreen ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Overlapped);
    }

    private void ApplyStoredMetadata()
    {
        foreach (var tile in ViewModel.Flattened)
        {
            tile.ApplyFavorite(_libraryState.IsFavorite(tile.Item.PrimaryPath));
            tile.ApplyAnnotation(_libraryState.GetAnnotation(tile.Item.PrimaryPath));
        }
        ApplyOrganizationFilters();
    }

    private void ApplyOrganizationFilters()
    {
        if (!_isInitialized) return;
        var sortOrder = (MediaSortOrder)Math.Max(0, SortBox.SelectedIndex);
        var albumMembers = _activeAlbum is null ? null : _libraryState.GetAlbumMembers(_activeAlbum);
        ViewModel.SetOrganization(FavoritesFilterButton.IsChecked == true, albumMembers, sortOrder);
        UpdateLibraryTitle();
        UpdateNavigationVisuals();
        UpdateLibraryStatus();
    }

    private void FavoritesFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        _activeSmartView = null;
        ApplyOrganizationFilters();
    }

    private void OrganizationFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        ApplyOrganizationFilters();
    }

    private void AlbumFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        _activeAlbum = AlbumFilterBox.SelectedIndex <= 0 ? null : AlbumFilterBox.SelectedItem as string;
        ApplyOrganizationFilters();
    }

    private void RefreshAlbumFilter(string? selectAlbum = null)
    {
        var wasInitialized = _isInitialized;
        _isInitialized = false;
        AlbumFilterBox.Items.Clear();
        AlbumFilterBox.Items.Add(L("AllAlbums"));
        foreach (var album in _libraryState.Albums) AlbumFilterBox.Items.Add(album);
        var selectedIndex = 0;
        if (selectAlbum is not null)
        {
            for (var index = 1; index < AlbumFilterBox.Items.Count; index++)
            {
                if (string.Equals(AlbumFilterBox.Items[index] as string, selectAlbum, StringComparison.CurrentCultureIgnoreCase)) selectedIndex = index;
            }
        }
        AlbumFilterBox.SelectedIndex = selectedIndex;
        _activeAlbum = selectedIndex == 0 ? null : AlbumFilterBox.Items[selectedIndex] as string;
        _isInitialized = wasInitialized;
    }

    private async void CreateAlbum_Click(object sender, RoutedEventArgs e) => await PromptCreateAlbumAsync();

    private async Task<string?> PromptCreateAlbumAsync()
    {
        var input = new TextBox { PlaceholderText = L("AlbumName"), MaxLength = 60 };
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = L("CreateAlbum"),
            Content = input,
            PrimaryButtonText = L("Create"),
            CloseButtonText = L("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;
        var name = input.Text.Trim();
        if (string.IsNullOrEmpty(name)) return null;
        if (!_libraryState.CreateAlbum(name))
        {
            await ShowMessageAsync(L("AlbumExists"), LF("AlbumExistsMessage", name));
            return null;
        }
        RefreshAlbumFilter(name);
        ApplyOrganizationFilters();
        return name;
    }

    private async void DeleteAlbum_Click(object sender, RoutedEventArgs e)
    {
        if (_activeAlbum is null)
        {
            await ShowMessageAsync(L("SelectAlbum"), L("SelectAlbumMessage"));
            return;
        }
        var album = _activeAlbum;
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = LF("DeleteAlbumTitle", album), Content = L("DeleteAlbumMessage"), PrimaryButtonText = L("Delete"), CloseButtonText = L("Cancel"), DefaultButton = ContentDialogButton.Close };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        _libraryState.DeleteAlbum(album);
        RefreshAlbumFilter();
        ApplyOrganizationFilters();
    }

    private async void AddToAlbum_Click(object sender, RoutedEventArgs e)
    {
        if (_viewerIndex < 0) return;
        if (_libraryState.Albums.Count == 0 && await PromptCreateAlbumAsync() is null) return;
        var albums = _libraryState.Albums;
        var picker = new ComboBox { ItemsSource = albums, SelectedIndex = 0, MinWidth = 260 };
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = L("AlbumMembership"),
            Content = new StackPanel { Spacing = 10, Children = { new TextBlock { Text = L("AlbumMembershipMessage") }, picker } },
            PrimaryButtonText = L("ToggleMembership"),
            CloseButtonText = L("Cancel"),
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || picker.SelectedItem is not string album) return;
        var tile = ViewModel.Flattened[_viewerIndex];
        var added = _libraryState.ToggleAlbumMembership(album, tile.Item.PrimaryPath);
        StatusText.Text = added ? LF("AddedAlbum", album) : LF("RemovedAlbum", album);
        ApplyOrganizationFilters();
    }

    private void Favorite_Click(object sender, RoutedEventArgs e) => ToggleCurrentFavorite();

    private void ToggleCurrentFavorite()
    {
        if (_viewerIndex < 0) return;
        var tile = ViewModel.Flattened[_viewerIndex];
        var isFavorite = _libraryState.ToggleFavorite(tile.Item.PrimaryPath);
        tile.ApplyFavorite(isFavorite);
        ViewModel.RefreshMetadata();
        FavoriteButton.Content = isFavorite ? "♥" : "♡";
        UpdateLibraryStatus();
    }

    private void DensityCompact_Click(object sender, RoutedEventArgs e) => SetGridDensity(118, 88);
    private void DensityComfortable_Click(object sender, RoutedEventArgs e) => SetGridDensity(150, 112);
    private void DensityLarge_Click(object sender, RoutedEventArgs e) => SetGridDensity(194, 146);

    private void SetGridDensity(double width, double height)
    {
        _tileWidth = width;
        ViewModel.SetTileSize(width, height);
        ViewModel.SetColumnCount(Math.Max(1, (int)(TimelineGrid.ActualWidth / (width + 3))));
        DensitySmallItem.IsChecked = width < 130;
        DensityMediumItem.IsChecked = width >= 130 && width < 180;
        DensityLargeItem.IsChecked = width >= 180;
    }

    private async void OpenRecentFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = UserSettings.LoadLastFolder();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            await ShowMessageAsync(L("NoRecent"), L("NoRecentMessage"));
            return;
        }
        await LoadFolderAsync(path);
    }

    private void SecondaryFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        ApplySecondaryFilters();
    }

    private void ApplySecondaryFilters()
    {
        var tag = TagFilterBox.SelectedIndex <= 0 ? null : TagFilterBox.SelectedItem as string;
        var extension = ExtensionFilterBox.SelectedIndex <= 0 ? null : ExtensionFilterBox.SelectedItem as string;
        var days = DateFilterBox.SelectedItem is ComboBoxItem { Tag: string value } && int.TryParse(value, out var parsed) ? parsed : 0;
        ViewModel.SetSecondaryFilter(extension, tag, days);
        UpdateLibraryStatus();
    }

    private void RefreshSecondaryFilters(string? selectTag = null, string? selectExtension = null)
    {
        var wasInitialized = _isInitialized;
        _isInitialized = false;
        TagFilterBox.Items.Clear();
        TagFilterBox.Items.Add(L("AnyTag"));
        foreach (var tag in _libraryState.Tags) TagFilterBox.Items.Add(tag);
        TagFilterBox.SelectedIndex = FindStringItemIndex(TagFilterBox, selectTag);
        ExtensionFilterBox.Items.Clear();
        ExtensionFilterBox.Items.Add(L("AnyFormat"));
        foreach (var extension in ViewModel.Flattened.Select(tile => tile.Extension).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)) ExtensionFilterBox.Items.Add(extension);
        ExtensionFilterBox.SelectedIndex = FindStringItemIndex(ExtensionFilterBox, selectExtension);
        _isInitialized = wasInitialized;
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        SearchBox.Text = string.Empty;
        FavoritesFilterButton.IsChecked = false;
        AlbumFilterBox.SelectedIndex = 0;
        SortBox.SelectedIndex = 0;
        TagFilterBox.SelectedIndex = 0;
        ExtensionFilterBox.SelectedIndex = 0;
        DateFilterBox.SelectedIndex = 0;
        SmartViewBox.SelectedIndex = 0;
        _currentFilter = null;
        _activeAlbum = null;
        _activeSmartView = null;
        LibraryTitleText.Text = L("AllMedia");
        _isInitialized = true;
        ViewModel.SetFilter(null, string.Empty);
        ApplyOrganizationFilters();
        ApplySecondaryFilters();
        UpdateNavigationVisuals();
    }

    private void RefreshSmartViews(string? selectName = null)
    {
        var wasInitialized = _isInitialized;
        _isInitialized = false;
        SmartViewBox.Items.Clear();
        SmartViewBox.Items.Add(L("SmartCollections"));
        foreach (var view in _libraryState.SavedViews) SmartViewBox.Items.Add(view.Name);
        SmartViewBox.SelectedIndex = FindStringItemIndex(SmartViewBox, selectName);
        _activeSmartView = SmartViewBox.SelectedIndex <= 0 ? null : SmartViewBox.SelectedItem as string;
        _isInitialized = wasInitialized;
    }

    private async void SaveSmartView_Click(object sender, RoutedEventArgs e)
    {
        var input = new TextBox { PlaceholderText = L("ViewName"), MaxLength = 60 };
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = L("SaveFilters"), Content = input, PrimaryButtonText = L("Save"), CloseButtonText = L("Cancel"), DefaultButton = ContentDialogButton.Primary };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(input.Text)) return;
        var view = new SavedLibraryView(
            input.Text.Trim(), SearchBox.Text.Trim(), _currentFilter, FavoritesFilterButton.IsChecked == true,
            _activeAlbum,
            TagFilterBox.SelectedIndex <= 0 ? null : TagFilterBox.SelectedItem as string,
            ExtensionFilterBox.SelectedIndex <= 0 ? null : ExtensionFilterBox.SelectedItem as string,
            DateFilterBox.SelectedItem is ComboBoxItem { Tag: string dayTag } && int.TryParse(dayTag, out var days) ? days : 0,
            Math.Max(0, SortBox.SelectedIndex));
        if (!_libraryState.SaveView(view))
        {
            await ShowMessageAsync(L("NameUsed"), L("NameUsedMessage"));
            return;
        }
        RefreshSmartViews(view.Name);
        LibraryTitleText.Text = view.Name;
    }

    private async void DeleteSmartView_Click(object sender, RoutedEventArgs e)
    {
        if (_activeSmartView is null)
        {
            await ShowMessageAsync(L("SelectView"), L("SelectViewMessage"));
            return;
        }
        _libraryState.DeleteView(_activeSmartView);
        _activeSmartView = null;
        RefreshSmartViews();
    }

    private void SmartViewBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || SmartViewBox.SelectedIndex <= 0 || SmartViewBox.SelectedItem is not string name) return;
        var view = _libraryState.SavedViews.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase));
        if (view is null) return;
        _activeSmartView = view.Name;
        _isInitialized = false;
        SearchBox.Text = view.SearchQuery;
        FavoritesFilterButton.IsChecked = view.FavoritesOnly;
        SortBox.SelectedIndex = Math.Clamp(view.SortOrder, 0, SortBox.Items.Count - 1);
        AlbumFilterBox.SelectedIndex = FindStringItemIndex(AlbumFilterBox, view.Album);
        TagFilterBox.SelectedIndex = FindStringItemIndex(TagFilterBox, view.Tag);
        ExtensionFilterBox.SelectedIndex = FindStringItemIndex(ExtensionFilterBox, view.Extension);
        DateFilterBox.SelectedIndex = FindTaggedItemIndex(DateFilterBox, view.DateRangeDays);
        _activeAlbum = view.Album;
        _currentFilter = view.Kind;
        _isInitialized = true;
        LibraryTitleText.Text = view.Name;
        ViewModel.SetFilter(view.Kind, view.SearchQuery);
        ApplyOrganizationFilters();
        ApplySecondaryFilters();
    }

    private static int FindStringItemIndex(ComboBox comboBox, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        for (var index = 1; index < comboBox.Items.Count; index++)
        {
            if (string.Equals(comboBox.Items[index] as string, value, StringComparison.CurrentCultureIgnoreCase)) return index;
        }
        return 0;
    }

    private static int FindTaggedItemIndex(ComboBox comboBox, int value)
    {
        for (var index = 0; index < comboBox.Items.Count; index++)
        {
            if (comboBox.Items[index] is ComboBoxItem { Tag: string tag } && int.TryParse(tag, out var parsed) && parsed == value) return index;
        }
        return 0;
    }

    private async void EditAnnotation_Click(object sender, RoutedEventArgs e)
    {
        if (_viewerIndex < 0) return;
        var tile = ViewModel.Flattened[_viewerIndex];
        var tags = new TextBox { Header = L("Tags"), Text = string.Join(", ", tile.Tags), PlaceholderText = L("TagPlaceholder") };
        var note = new TextBox { Header = L("Note"), Text = tile.Note, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 130, PlaceholderText = L("NotePlaceholder") };
        var content = new StackPanel { Spacing = 14, Children = { tags, note, new TextBlock { Text = L("LocalOnly"), Opacity = 0.65 } } };
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = L("TagsAndNote"), Content = content, PrimaryButtonText = L("Save"), CloseButtonText = L("Cancel"), DefaultButton = ContentDialogButton.Primary };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        _libraryState.SetAnnotation(tile.Item.PrimaryPath, note.Text, tags.Text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries));
        tile.ApplyAnnotation(_libraryState.GetAnnotation(tile.Item.PrimaryPath));
        RefreshSecondaryFilters();
        ViewModel.RefreshMetadata();
        ShowCurrentItem();
    }

    private void SelectionMode_Click(object sender, RoutedEventArgs e)
    {
        _isSelectionMode = !_isSelectionMode;
        ViewModel.SetSelectionMode(_isSelectionMode);
        BatchBar.Visibility = _isSelectionMode ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(SelectionModeButton, _isSelectionMode ? L("Cancel") : L("Select"));
        UpdateSelectionUi();
    }

    private void UpdateSelectionUi()
    {
        SelectionCountText.Text = LF("SelectedCount", ViewModel.SelectedItems.Count);
    }

    private void SelectAllVisible_Click(object sender, RoutedEventArgs e)
    {
        foreach (var tile in ViewModel.VisibleItems.Where(tile => !tile.IsSelected)) tile.ToggleSelected();
        UpdateSelectionUi();
    }

    private void BatchFavorite_Click(object sender, RoutedEventArgs e)
    {
        var selected = ViewModel.SelectedItems;
        if (selected.Count == 0) return;
        _libraryState.SetFavorites(selected.Select(tile => tile.Item.PrimaryPath), true);
        foreach (var tile in selected) tile.ApplyFavorite(true);
        ViewModel.RefreshMetadata();
        UpdateLibraryStatus();
    }

    private async void BatchAlbum_Click(object sender, RoutedEventArgs e)
    {
        var selected = ViewModel.SelectedItems;
        if (selected.Count == 0) return;
        if (_libraryState.Albums.Count == 0 && await PromptCreateAlbumAsync() is null) return;
        var picker = new ComboBox { ItemsSource = _libraryState.Albums, SelectedIndex = 0, MinWidth = 260 };
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = L("AddSelectionAlbum"), Content = picker, PrimaryButtonText = L("Add"), CloseButtonText = L("Cancel") };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || picker.SelectedItem is not string album) return;
        _libraryState.AddAlbumMembers(album, selected.Select(tile => tile.Item.PrimaryPath));
        ApplyOrganizationFilters();
        StatusText.Text = $"Added {selected.Count:N0} items to {album}";
    }

    private async void ExportSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = ViewModel.SelectedItems;
        if (selected.Count == 0) return;
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;
        if (IsSameOrChildPath(folder.Path, _currentFolderPath ?? string.Empty))
        {
            await ShowMessageAsync("Choose a different destination", "To keep the source library read-only, export copies outside the currently opened folder.");
            return;
        }
        ScanProgress.Visibility = Visibility.Visible;
        ScanProgress.IsActive = true;
        try
        {
            var progress = new Progress<(int Completed, int Total)>(value => StatusText.Text = $"Exporting {value.Completed:N0} of {value.Total:N0} files…");
            var result = await _exporter.ExportAsync(selected.Select(tile => tile.Item), folder.Path, progress, CancellationToken.None);
            StatusText.Text = $"Exported {result.MediaItems:N0} items ({result.Files:N0} files, {FormatBytes(result.Bytes)})";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await ShowMessageAsync("Export could not finish", exception.Message);
        }
        finally
        {
            ScanProgress.IsActive = false;
            ScanProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void Insights_Click(object sender, RoutedEventArgs e)
    {
        UpdateInsights();
        InsightsPanel.Visibility = Visibility.Visible;
    }

    private void CloseInsights_Click(object sender, RoutedEventArgs e) => InsightsPanel.Visibility = Visibility.Collapsed;

    private void UpdateInsights()
    {
        var items = ViewModel.Flattened;
        var photos = items.Count(tile => tile.Item.Kind == MediaKind.Photo);
        var livePhotos = items.Count(tile => tile.Item.Kind == MediaKind.LivePhoto);
        var videos = items.Count(tile => tile.Item.Kind == MediaKind.Video);
        var hdr = items.Count(tile => tile.Item.DynamicRange != DynamicRange.Standard);
        var scaleMaximum = Math.Max(1, items.Count);

        InsightTotalText.Text = items.Count.ToString("N0");
        InsightFavoritesText.Text = items.Count(tile => tile.IsFavorite).ToString("N0");
        InsightAlbumCountText.Text = _libraryState.Albums.Count.ToString("N0");
        InsightPhotosText.Text = photos.ToString("N0"); InsightPhotosBar.Maximum = scaleMaximum; InsightPhotosBar.Value = photos;
        InsightLiveText.Text = livePhotos.ToString("N0"); InsightLiveBar.Maximum = scaleMaximum; InsightLiveBar.Value = livePhotos;
        InsightVideosText.Text = videos.ToString("N0"); InsightVideosBar.Maximum = scaleMaximum; InsightVideosBar.Value = videos;
        InsightHdrText.Text = hdr.ToString("N0"); InsightHdrBar.Maximum = scaleMaximum; InsightHdrBar.Value = hdr;
        InsightDateText.Text = items.Count == 0 ? L("NoDates") : $"{items.Min(tile => tile.Item.CapturedAt).LocalDateTime:d} – {items.Max(tile => tile.Item.CapturedAt).LocalDateTime:d}";
        InsightStorageText.Text = FormatBytes(items.Sum(tile => TryGetMediaSize(tile.Item)));
        InsightAlbumsText.Text = _libraryState.Albums.Count == 0
            ? L("NoAlbums")
            : string.Join("\n", _libraryState.Albums
                .Select(album => (Name: album, Count: _libraryState.GetAlbumMembers(album).Count))
                .OrderByDescending(album => album.Count)
                .ThenBy(album => album.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(6)
                .Select(album => LF("AlbumCount", album.Name, album.Count)));
    }

    private void Slideshow_Click(object sender, RoutedEventArgs e) => ToggleSlideshow();

    private void ToggleSlideshow()
    {
        if (_slideshowTimer.IsEnabled) StopSlideshow();
        else
        {
            if (_viewerIndex < 0 || ViewModel.VisibleItems.Count == 0) return;
            _slideshowTimer.Start();
            SlideshowButton.Content = L("StopShow");
        }
    }

    private void StopSlideshow()
    {
        _slideshowTimer.Stop();
        SlideshowButton.Content = L("Slideshow");
    }

    private void SlideshowTimer_Tick(object? sender, object e)
    {
        var items = ViewModel.VisibleItems;
        if (items.Count == 0) { StopSlideshow(); return; }
        MediaTileViewModel next;
        if (ShuffleButton.IsChecked == true)
        {
            next = items[_random.Next(items.Count)];
        }
        else
        {
            var current = _viewerIndex < 0 ? -1 : items.ToList().IndexOf(ViewModel.Flattened[_viewerIndex]);
            if (current + 1 >= items.Count)
            {
                if (LoopButton.IsChecked != true) { StopSlideshow(); return; }
                current = -1;
            }
            next = items[current + 1];
        }
        OpenViewer(ViewModel.IndexOf(next));
    }

    private void SlideshowInterval_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SlideshowIntervalBox.SelectedItem is not ComboBoxItem { Tag: string tag } || !double.TryParse(tag, out var seconds)) return;
        _slideshowTimer.Interval = TimeSpan.FromSeconds(seconds);
        if (_isInitialized) UserSettings.SaveSlideshowSeconds(seconds);
    }

    private void RestoreSlideshowInterval()
    {
        var saved = UserSettings.LoadSlideshowSeconds();
        for (var index = 0; index < SlideshowIntervalBox.Items.Count; index++)
        {
            if (SlideshowIntervalBox.Items[index] is ComboBoxItem { Tag: string tag } && double.TryParse(tag, out var seconds) && Math.Abs(seconds - saved) < 0.1)
            {
                SlideshowIntervalBox.SelectedIndex = index;
                _slideshowTimer.Interval = TimeSpan.FromSeconds(seconds);
                return;
            }
        }
        _slideshowTimer.Interval = TimeSpan.FromSeconds(3);
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (_viewerIndex < 0) return;
        var package = new DataPackage();
        package.SetText(ViewModel.Flattened[_viewerIndex].Item.PrimaryPath);
        Clipboard.SetContent(package);
        ViewerPosition.Text = L("PathCopied");
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = title, Content = message, CloseButtonText = L("Ok") };
        await dialog.ShowAsync();
    }

    private static long TryGetMediaSize(MediaItem item)
    {
        try
        {
            var total = new FileInfo(item.PrimaryPath).Length;
            if (item.MotionPath is { } motionPath) total += new FileInfo(motionPath).Length;
            return total;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FileNotFoundException)
        {
            return 0;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.#} {units[unit]}";
    }

    private static bool IsSameOrChildPath(string candidate, string parent)
    {
        if (string.IsNullOrWhiteSpace(parent)) return false;
        try
        {
            var parentPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
            var candidatePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
            return candidatePath.Equals(parentPath, StringComparison.OrdinalIgnoreCase)
                || candidatePath.StartsWith(parentPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException) { return false; }
    }

    private sealed class BulkObservableCollection<T> : ObservableCollection<T>
    {
        public void ReplaceAll(IEnumerable<T> items)
        {
            Items.Clear();
            foreach (var item in items) Items.Add(item);
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new System.Collections.Specialized.NotifyCollectionChangedEventArgs(
                System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
        }
    }
}
