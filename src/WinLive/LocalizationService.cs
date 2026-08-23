using System.Globalization;

namespace WinLive;

internal enum AppLanguageMode
{
    System,
    English,
    SimplifiedChinese,
}

internal static class LocalizationService
{
    private static readonly CultureInfo SystemCulture = CultureInfo.InstalledUICulture;
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["Library"] = "Library", ["AllMedia"] = "All media", ["Photos"] = "Photos", ["LivePhotos"] = "Live Photos", ["Videos"] = "Videos", ["Favorites"] = "Favorites",
        ["OpenFolder"] = "Open folder", ["Search"] = "Search", ["SearchPlaceholder"] = "Search filenames, tags and notes", ["Filter"] = "Filter", ["Select"] = "Select", ["Cancel"] = "Cancel", ["More"] = "More",
        ["NoFolder"] = "No folder selected", ["ChooseFolder"] = "Choose a folder", ["ChooseAnotherFolder"] = "Choose another folder", ["EmptyTitle"] = "No library open", ["EmptyDescription"] = "Choose a folder to start browsing.",
        ["NoMediaTitle"] = "No media found", ["NoMediaDescription"] = "Choose another folder or add media here.", ["NoMatchesTitle"] = "No matches", ["NoMatchesDescription"] = "Try another search or clear the filters.", ["ClearFilters"] = "Clear filters",
        ["Scanning"] = "Scanning…", ["ScanCancelled"] = "Scan cancelled", ["Items"] = "{0:N0} items", ["TotalItems"] = "Items", ["FilteredItems"] = "{0:N0} of {1:N0}", ["NoSupportedMedia"] = "No supported media",
        ["SortNewest"] = "Newest first", ["SortOldest"] = "Oldest first", ["SortNameAZ"] = "Name A–Z", ["SortNameZA"] = "Name Z–A",
        ["FilterTitle"] = "Filters", ["Album"] = "Album", ["AllAlbums"] = "All albums", ["SmartCollection"] = "Saved view", ["SmartCollections"] = "Saved views", ["AnyTag"] = "Any tag", ["AnyFormat"] = "Any format", ["AnyDate"] = "Any date", ["Last7"] = "Last 7 days", ["Last30"] = "Last 30 days", ["LastYear"] = "Last year",
        ["NewAlbum"] = "New album", ["DeleteAlbum"] = "Delete album", ["SaveView"] = "Save view", ["DeleteView"] = "Delete view",
        ["Insights"] = "Library insights", ["Shortcuts"] = "Keyboard shortcuts", ["Settings"] = "Settings", ["Language"] = "Language", ["Theme"] = "Theme", ["System"] = "Follow system", ["English"] = "English", ["Chinese"] = "简体中文", ["Light"] = "Light", ["Dark"] = "Dark", ["RecentFolder"] = "Recent folder", ["Group"] = "Group", ["GroupNone"] = "No grouping", ["GroupDay"] = "Day", ["GroupMonth"] = "Month", ["GroupYear"] = "Year", ["GallerySize"] = "Thumbnail size", ["Small"] = "Small", ["Medium"] = "Medium", ["Large"] = "Large",
        ["Details"] = "Details", ["Open"] = "Open", ["Favorite"] = "Favorite", ["Unfavorite"] = "Remove from favorites", ["AddAlbum"] = "Add to album", ["EditTags"] = "Edit tags and note", ["CopyPath"] = "Copy path", ["Reveal"] = "Show in folder", ["Slideshow"] = "Slideshow", ["StopShow"] = "Stop slideshow", ["Mute"] = "Mute", ["Unmute"] = "Unmute", ["Fit"] = "Fit", ["Previous"] = "Previous", ["Next"] = "Next", ["Back"] = "Back", ["PlayPause"] = "Play or pause", ["ViewerCount"] = "{0:N0} / {1:N0}",
        ["Captured"] = "Captured", ["Size"] = "Size", ["Tags"] = "Tags", ["Note"] = "Note", ["None"] = "None", ["Duration"] = "Duration",
        ["SelectedCount"] = "{0:N0} selected", ["SelectVisible"] = "Select visible", ["ExportCopies"] = "Export copies", ["Done"] = "Done",
        ["CreateAlbum"] = "Create album", ["AlbumName"] = "Album name", ["Create"] = "Create", ["Delete"] = "Delete", ["Save"] = "Save", ["Close"] = "Close", ["Ok"] = "OK",
        ["SlideshowOptions"] = "Slideshow options", ["ChangeEvery"] = "Change every", ["Seconds2"] = "2 seconds", ["Seconds3"] = "3 seconds", ["Seconds5"] = "5 seconds", ["Seconds10"] = "10 seconds", ["Shuffle"] = "Shuffle", ["Loop"] = "Loop",
        ["AlbumExists"] = "Album already exists", ["AlbumExistsMessage"] = "An album named “{0}” already exists.", ["SelectAlbum"] = "Select an album", ["SelectAlbumMessage"] = "Choose an album first.", ["DeleteAlbumTitle"] = "Delete “{0}”?", ["DeleteAlbumMessage"] = "The media files will not be deleted.", ["AlbumMembership"] = "Album membership", ["AlbumMembershipMessage"] = "Choose an album to add or remove this item.", ["ToggleMembership"] = "Toggle membership", ["AddedAlbum"] = "Added to {0}", ["RemovedAlbum"] = "Removed from {0}",
        ["NoRecent"] = "No recent folder", ["NoRecentMessage"] = "Open a folder once and it will appear here.", ["ViewName"] = "Saved view name", ["SaveFilters"] = "Save current filters", ["NameUsed"] = "Name already used", ["NameUsedMessage"] = "Choose a different saved view name.", ["SelectView"] = "Select a saved view", ["SelectViewMessage"] = "Choose a saved view first.",
        ["TagsAndNote"] = "Tags and note", ["TagPlaceholder"] = "travel, family, portfolio", ["NotePlaceholder"] = "Add a private note…", ["LocalOnly"] = "Stored locally; source metadata is unchanged.", ["AddSelectionAlbum"] = "Add selection to album", ["Add"] = "Add",
        ["PhotosCount"] = "{0:N0} photos", ["LiveCount"] = "{0:N0} Live Photos", ["VideosCount"] = "{0:N0} videos", ["HdrCount"] = "{0:N0} HDR items", ["NoDates"] = "No capture dates", ["NoAlbums"] = "No custom albums", ["AlbumCount"] = "{0}: {1:N0} items",
        ["PathCopied"] = "Path copied",
        ["FilterSubtitle"] = "Narrow this view", ["Collections"] = "Collections", ["Date"] = "Date", ["Metadata"] = "Metadata", ["General"] = "General", ["Keyboard"] = "Keyboard", ["AppearanceLanguage"] = "Appearance and language", ["PreviousNext"] = "Previous / next", ["ZoomFit"] = "Zoom / fit", ["Fullscreen"] = "Full screen", ["Organize"] = "ORGANIZE", ["View"] = "VIEW", ["File"] = "FILE", ["Preferences"] = "PREFERENCES", ["WinLivePreferences"] = "WinLive preferences", ["ApplyImmediately"] = "Changes apply immediately.", ["LanguageDescription"] = "Language used throughout WinLive", ["ThemeDescription"] = "Light, dark, or match Windows", ["ShortcutDescription"] = "Fast access to common library and viewer actions.", ["ThumbnailShortcut"] = "Change thumbnail size", ["ShortcutReference"] = "Shortcut reference",
        ["About"] = "About", ["AboutDescription"] = "A fast, local-first library for Live Photos and everyday images.", ["Version"] = "Version", ["LocalFirst"] = "Local-first", ["LocalFirstDescription"] = "Your library, favorites, albums, and notes stay on this device.", ["OpenSource"] = "Open source", ["GitHubDescription"] = "View the source, report issues, or contribute on GitHub.", ["GitHubProject"] = "GitHub",
        ["InsightsSubtitle"] = "A clear snapshot of this library", ["Overview"] = "Overview", ["Albums"] = "Albums", ["MediaBreakdown"] = "Media breakdown", ["DateRange"] = "Date range", ["Storage"] = "Storage",
    };

    private static readonly IReadOnlyDictionary<string, string> Chinese = new Dictionary<string, string>
    {
        ["Library"] = "图库", ["AllMedia"] = "全部", ["Photos"] = "照片", ["LivePhotos"] = "实况照片", ["Videos"] = "视频", ["Favorites"] = "收藏",
        ["OpenFolder"] = "打开文件夹", ["Search"] = "搜索", ["SearchPlaceholder"] = "搜索文件名、标签和备注", ["Filter"] = "筛选", ["Select"] = "选择", ["Cancel"] = "取消", ["More"] = "更多",
        ["NoFolder"] = "尚未打开文件夹", ["ChooseFolder"] = "选择文件夹", ["ChooseAnotherFolder"] = "选择其他文件夹", ["EmptyTitle"] = "尚未打开图库", ["EmptyDescription"] = "选择一个文件夹即可开始浏览。",
        ["NoMediaTitle"] = "未找到媒体", ["NoMediaDescription"] = "请选择其他文件夹，或在这里添加媒体。", ["NoMatchesTitle"] = "没有匹配项", ["NoMatchesDescription"] = "请更换搜索词或清除筛选。", ["ClearFilters"] = "清除筛选",
        ["Scanning"] = "正在扫描…", ["ScanCancelled"] = "扫描已取消", ["Items"] = "{0:N0} 项", ["TotalItems"] = "项目", ["FilteredItems"] = "显示 {0:N0} / {1:N0} 项", ["NoSupportedMedia"] = "没有支持的媒体",
        ["SortNewest"] = "最新优先", ["SortOldest"] = "最早优先", ["SortNameAZ"] = "名称 A–Z", ["SortNameZA"] = "名称 Z–A",
        ["FilterTitle"] = "筛选", ["Album"] = "相册", ["AllAlbums"] = "全部相册", ["SmartCollection"] = "已存视图", ["SmartCollections"] = "已存视图", ["AnyTag"] = "不限标签", ["AnyFormat"] = "不限格式", ["AnyDate"] = "不限日期", ["Last7"] = "最近 7 天", ["Last30"] = "最近 30 天", ["LastYear"] = "最近一年",
        ["NewAlbum"] = "新建相册", ["DeleteAlbum"] = "删除相册", ["SaveView"] = "保存视图", ["DeleteView"] = "删除视图",
        ["Insights"] = "图库概览", ["Shortcuts"] = "键盘快捷键", ["Settings"] = "设置", ["Language"] = "语言", ["Theme"] = "主题", ["System"] = "跟随系统", ["English"] = "English", ["Chinese"] = "简体中文", ["Light"] = "亮色", ["Dark"] = "暗色", ["RecentFolder"] = "最近文件夹", ["Group"] = "分组", ["GroupNone"] = "不分组", ["GroupDay"] = "按日", ["GroupMonth"] = "按月", ["GroupYear"] = "按年", ["GallerySize"] = "缩略图大小", ["Small"] = "小", ["Medium"] = "中", ["Large"] = "大",
        ["Details"] = "详细信息", ["Open"] = "打开", ["Favorite"] = "收藏", ["Unfavorite"] = "取消收藏", ["AddAlbum"] = "添加到相册", ["EditTags"] = "编辑标签和备注", ["CopyPath"] = "复制路径", ["Reveal"] = "在文件夹中显示", ["Slideshow"] = "幻灯片", ["StopShow"] = "停止幻灯片", ["Mute"] = "静音", ["Unmute"] = "取消静音", ["Fit"] = "适应窗口", ["Previous"] = "上一项", ["Next"] = "下一项", ["Back"] = "返回", ["PlayPause"] = "播放或暂停", ["ViewerCount"] = "{0:N0} / {1:N0}",
        ["Captured"] = "拍摄时间", ["Size"] = "大小", ["Tags"] = "标签", ["Note"] = "备注", ["None"] = "无", ["Duration"] = "时长",
        ["SelectedCount"] = "已选择 {0:N0} 项", ["SelectVisible"] = "选择当前结果", ["ExportCopies"] = "导出副本", ["Done"] = "完成",
        ["CreateAlbum"] = "新建相册", ["AlbumName"] = "相册名称", ["Create"] = "创建", ["Delete"] = "删除", ["Save"] = "保存", ["Close"] = "关闭", ["Ok"] = "确定",
        ["SlideshowOptions"] = "幻灯片选项", ["ChangeEvery"] = "切换间隔", ["Seconds2"] = "2 秒", ["Seconds3"] = "3 秒", ["Seconds5"] = "5 秒", ["Seconds10"] = "10 秒", ["Shuffle"] = "随机播放", ["Loop"] = "循环播放",
        ["AlbumExists"] = "相册已存在", ["AlbumExistsMessage"] = "已存在名为“{0}”的相册。", ["SelectAlbum"] = "请选择相册", ["SelectAlbumMessage"] = "请先选择一个相册。", ["DeleteAlbumTitle"] = "删除“{0}”？", ["DeleteAlbumMessage"] = "媒体文件不会被删除。", ["AlbumMembership"] = "相册归属", ["AlbumMembershipMessage"] = "选择相册，将此项目加入或移出。", ["ToggleMembership"] = "切换归属", ["AddedAlbum"] = "已添加到 {0}", ["RemovedAlbum"] = "已从 {0} 移除",
        ["NoRecent"] = "没有最近文件夹", ["NoRecentMessage"] = "打开一次文件夹后，它会显示在这里。", ["ViewName"] = "视图名称", ["SaveFilters"] = "保存当前筛选", ["NameUsed"] = "名称已被使用", ["NameUsedMessage"] = "请使用其他视图名称。", ["SelectView"] = "请选择视图", ["SelectViewMessage"] = "请先选择一个已存视图。",
        ["TagsAndNote"] = "标签和备注", ["TagPlaceholder"] = "旅行、家人、作品", ["NotePlaceholder"] = "添加私人备注…", ["LocalOnly"] = "仅保存在本机，不会修改源文件元数据。", ["AddSelectionAlbum"] = "将所选项目添加到相册", ["Add"] = "添加",
        ["PhotosCount"] = "{0:N0} 张照片", ["LiveCount"] = "{0:N0} 张实况照片", ["VideosCount"] = "{0:N0} 个视频", ["HdrCount"] = "{0:N0} 个 HDR 项目", ["NoDates"] = "没有拍摄日期", ["NoAlbums"] = "没有自定义相册", ["AlbumCount"] = "{0}：{1:N0} 项",
        ["PathCopied"] = "路径已复制",
        ["FilterSubtitle"] = "缩小当前结果范围", ["Collections"] = "集合", ["Date"] = "日期", ["Metadata"] = "元数据", ["General"] = "常规", ["Keyboard"] = "键盘", ["AppearanceLanguage"] = "外观与语言", ["PreviousNext"] = "上一项 / 下一项", ["ZoomFit"] = "缩放 / 适应窗口", ["Fullscreen"] = "全屏", ["Organize"] = "整理", ["View"] = "查看", ["File"] = "文件", ["Preferences"] = "偏好设置", ["WinLivePreferences"] = "WinLive 偏好设置", ["ApplyImmediately"] = "更改会立即生效。", ["LanguageDescription"] = "WinLive 全局使用的界面语言", ["ThemeDescription"] = "选择亮色、暗色或跟随 Windows", ["ShortcutDescription"] = "快速使用图库和查看器的常用操作。", ["ThumbnailShortcut"] = "更改缩略图大小", ["ShortcutReference"] = "快捷键参考",
        ["About"] = "关于", ["AboutDescription"] = "快速、以本机为中心的实况照片与日常图像图库。", ["Version"] = "版本", ["LocalFirst"] = "数据留在本机", ["LocalFirstDescription"] = "图库、收藏、相册和备注均保存在这台设备上。", ["OpenSource"] = "开放源代码", ["GitHubDescription"] = "在 GitHub 查看源码、反馈问题或参与贡献。", ["GitHubProject"] = "GitHub",
        ["InsightsSubtitle"] = "清晰了解当前图库", ["Overview"] = "概览", ["Albums"] = "相册", ["MediaBreakdown"] = "媒体构成", ["DateRange"] = "日期范围", ["Storage"] = "占用空间",
    };

    public static AppLanguageMode Mode { get; private set; }
    public static bool IsChinese => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);

    public static void Apply(AppLanguageMode mode)
    {
        Mode = mode;
        var culture = mode switch
        {
            AppLanguageMode.English => CultureInfo.GetCultureInfo("en-US"),
            AppLanguageMode.SimplifiedChinese => CultureInfo.GetCultureInfo("zh-CN"),
            _ => SystemCulture,
        };
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    public static string Get(string key) => (IsChinese ? Chinese : English).TryGetValue(key, out var value) ? value : key;
    public static string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
