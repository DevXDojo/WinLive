using System.Text.Json;

namespace WinLive.Core;

/// <summary>
/// Stores user-authored library metadata outside the source folders. The viewer stays
/// read-only while favorites, albums, annotations and smart collections survive restarts.
/// </summary>
public sealed class MediaLibraryStateStore
{
    private readonly string _statePath;
    private readonly object _gate = new();
    private LibraryState _state;

    public MediaLibraryStateStore(string? statePath = null)
    {
        _statePath = statePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinLive",
            "library-state.json");
        _state = Load();
    }

    public IReadOnlyList<string> Albums
    {
        get
        {
            lock (_gate)
            {
                return _state.Albums.Keys.Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
            }
        }
    }

    public IReadOnlyList<string> Tags
    {
        get
        {
            lock (_gate)
            {
                return _state.Annotations.Values.SelectMany(value => value.Tags).Distinct(StringComparer.CurrentCultureIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
            }
        }
    }

    public IReadOnlyList<SavedLibraryView> SavedViews
    {
        get
        {
            lock (_gate) return _state.SavedViews.OrderBy(view => view.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }
    }

    public bool IsFavorite(string path)
    {
        lock (_gate) return _state.Favorites.Contains(path, StringComparer.OrdinalIgnoreCase);
    }

    public bool ToggleFavorite(string path)
    {
        lock (_gate)
        {
            var existing = _state.Favorites.FindIndex(value => string.Equals(value, path, StringComparison.OrdinalIgnoreCase));
            var isFavorite = existing < 0;
            if (isFavorite) _state.Favorites.Add(path);
            else _state.Favorites.RemoveAt(existing);
            Save();
            return isFavorite;
        }
    }

    public void SetFavorite(string path, bool isFavorite)
    {
        lock (_gate)
        {
            var existing = _state.Favorites.FindIndex(value => string.Equals(value, path, StringComparison.OrdinalIgnoreCase));
            if (isFavorite && existing < 0) _state.Favorites.Add(path);
            if (!isFavorite && existing >= 0) _state.Favorites.RemoveAt(existing);
            Save();
        }
    }

    public void SetFavorites(IEnumerable<string> paths, bool isFavorite)
    {
        lock (_gate)
        {
            foreach (var path in paths)
            {
                var existing = _state.Favorites.FindIndex(value => string.Equals(value, path, StringComparison.OrdinalIgnoreCase));
                if (isFavorite && existing < 0) _state.Favorites.Add(path);
                if (!isFavorite && existing >= 0) _state.Favorites.RemoveAt(existing);
            }
            Save();
        }
    }

    public bool CreateAlbum(string name)
    {
        name = ValidateAlbumName(name);
        lock (_gate)
        {
            if (_state.Albums.Keys.Any(value => string.Equals(value, name, StringComparison.CurrentCultureIgnoreCase))) return false;
            _state.Albums[name] = [];
            Save();
            return true;
        }
    }

    public bool DeleteAlbum(string name)
    {
        lock (_gate)
        {
            var key = FindAlbumKey(name);
            if (key is null) return false;
            _state.Albums.Remove(key);
            Save();
            return true;
        }
    }

    public bool IsInAlbum(string name, string path)
    {
        lock (_gate)
        {
            var key = FindAlbumKey(name);
            return key is not null && _state.Albums[key].Contains(path, StringComparer.OrdinalIgnoreCase);
        }
    }

    public bool ToggleAlbumMembership(string name, string path)
    {
        lock (_gate)
        {
            var key = FindAlbumKey(name) ?? throw new KeyNotFoundException($"Album '{name}' was not found.");
            var members = _state.Albums[key];
            var existing = members.FindIndex(value => string.Equals(value, path, StringComparison.OrdinalIgnoreCase));
            var added = existing < 0;
            if (added) members.Add(path);
            else members.RemoveAt(existing);
            Save();
            return added;
        }
    }

    public void SetAlbumMembership(string name, string path, bool isMember)
    {
        lock (_gate)
        {
            var key = FindAlbumKey(name) ?? throw new KeyNotFoundException($"Album '{name}' was not found.");
            var members = _state.Albums[key];
            var existing = members.FindIndex(value => string.Equals(value, path, StringComparison.OrdinalIgnoreCase));
            if (isMember && existing < 0) members.Add(path);
            if (!isMember && existing >= 0) members.RemoveAt(existing);
            Save();
        }
    }

    public void AddAlbumMembers(string name, IEnumerable<string> paths)
    {
        lock (_gate)
        {
            var key = FindAlbumKey(name) ?? throw new KeyNotFoundException($"Album '{name}' was not found.");
            var members = _state.Albums[key];
            foreach (var path in paths.Where(path => !members.Contains(path, StringComparer.OrdinalIgnoreCase))) members.Add(path);
            Save();
        }
    }

    public MediaAnnotation GetAnnotation(string path)
    {
        lock (_gate)
        {
            var entry = _state.Annotations.FirstOrDefault(pair => string.Equals(pair.Key, path, StringComparison.OrdinalIgnoreCase)).Value;
            return entry is null ? MediaAnnotation.Empty : new MediaAnnotation(entry.Note, entry.Tags.ToArray());
        }
    }

    public void SetAnnotation(string path, string? note, IEnumerable<string>? tags)
    {
        var cleanNote = (note ?? string.Empty).Trim();
        var cleanTags = (tags ?? [])
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Take(20)
            .ToList();
        lock (_gate)
        {
            RemoveCaseInsensitive(_state.Annotations, path);
            if (cleanNote.Length > 0 || cleanTags.Count > 0) _state.Annotations[path] = new AnnotationState { Note = cleanNote, Tags = cleanTags };
            Save();
        }
    }

    public bool SaveView(SavedLibraryView view)
    {
        if (string.IsNullOrWhiteSpace(view.Name)) throw new ArgumentException("A smart collection needs a name.", nameof(view));
        lock (_gate)
        {
            if (_state.SavedViews.Any(item => string.Equals(item.Name, view.Name.Trim(), StringComparison.CurrentCultureIgnoreCase))) return false;
            _state.SavedViews.Add(view with { Name = view.Name.Trim() });
            Save();
            return true;
        }
    }

    public bool DeleteView(string name)
    {
        lock (_gate)
        {
            var index = _state.SavedViews.FindIndex(view => string.Equals(view.Name, name, StringComparison.CurrentCultureIgnoreCase));
            if (index < 0) return false;
            _state.SavedViews.RemoveAt(index);
            Save();
            return true;
        }
    }

    public IReadOnlySet<string> GetAlbumMembers(string name)
    {
        lock (_gate)
        {
            var key = FindAlbumKey(name);
            return key is null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : _state.Albums[key].ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }

    private string? FindAlbumKey(string name) =>
        _state.Albums.Keys.FirstOrDefault(value => string.Equals(value, name, StringComparison.CurrentCultureIgnoreCase));

    private LibraryState Load()
    {
        try
        {
            if (!File.Exists(_statePath)) return new LibraryState();
            return JsonSerializer.Deserialize<LibraryState>(File.ReadAllText(_statePath)) ?? new LibraryState();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new LibraryState();
        }
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_statePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(_statePath, JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string ValidateAlbumName(string name)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 60) throw new ArgumentException("Album names must contain 1–60 characters.", nameof(name));
        return name;
    }

    private static void RemoveCaseInsensitive(Dictionary<string, int> values, string key)
    {
        var existing = values.Keys.FirstOrDefault(value => string.Equals(value, key, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) values.Remove(existing);
    }

    private static void RemoveCaseInsensitive<TValue>(Dictionary<string, TValue> values, string key)
    {
        var existing = values.Keys.FirstOrDefault(value => string.Equals(value, key, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) values.Remove(existing);
    }

    private sealed class LibraryState
    {
        public List<string> Favorites { get; init; } = [];
        public Dictionary<string, List<string>> Albums { get; init; } = new(StringComparer.CurrentCultureIgnoreCase);
        public Dictionary<string, AnnotationState> Annotations { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public List<SavedLibraryView> SavedViews { get; init; } = [];
    }

    private sealed class AnnotationState
    {
        public string Note { get; init; } = string.Empty;
        public List<string> Tags { get; init; } = [];
    }
}

public sealed record MediaAnnotation(string Note, IReadOnlyList<string> Tags)
{
    public static MediaAnnotation Empty { get; } = new(string.Empty, []);
}

public sealed record SavedLibraryView(
    string Name,
    string SearchQuery,
    MediaKind? Kind,
    bool FavoritesOnly,
    string? Album,
    string? Tag,
    string? Extension,
    int DateRangeDays,
    int SortOrder);
