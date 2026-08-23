using WinLive.Core;
using Xunit;

namespace WinLive.Tests;

public sealed class MediaLibraryStateStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "WinLive.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Persists_favorites_and_album_membership()
    {
        var path = Path.Combine(_folder, "state.json");
        var mediaPath = Path.Combine(_folder, "photo.jpg");
        var store = new MediaLibraryStateStore(path);

        Assert.True(store.ToggleFavorite(mediaPath));
        Assert.True(store.CreateAlbum("Road trip"));
        Assert.True(store.ToggleAlbumMembership("Road trip", mediaPath));

        var reloaded = new MediaLibraryStateStore(path);
        Assert.True(reloaded.IsFavorite(mediaPath.ToUpperInvariant()));
        Assert.True(reloaded.IsInAlbum("ROAD TRIP", mediaPath));
    }

    [Fact]
    public void Toggle_operations_remove_existing_values()
    {
        var store = new MediaLibraryStateStore(Path.Combine(_folder, "state.json"));
        const string mediaPath = "C:\\photos\\one.jpg";
        store.ToggleFavorite(mediaPath);
        store.CreateAlbum("Keepers");
        store.ToggleAlbumMembership("Keepers", mediaPath);

        Assert.False(store.ToggleFavorite(mediaPath));
        Assert.False(store.ToggleAlbumMembership("Keepers", mediaPath));
        Assert.False(store.IsFavorite(mediaPath));
        Assert.False(store.IsInAlbum("Keepers", mediaPath));
    }

    [Fact]
    public void Album_can_be_deleted_without_touching_media()
    {
        var store = new MediaLibraryStateStore(Path.Combine(_folder, "state.json"));
        store.CreateAlbum("Temporary");
        Assert.True(store.DeleteAlbum("temporary"));
        Assert.Empty(store.Albums);
    }

    [Fact]
    public void Persists_private_annotations_and_smart_collections()
    {
        var statePath = Path.Combine(_folder, "state.json");
        var store = new MediaLibraryStateStore(statePath);
        store.SetAnnotation("photo.jpg", "Portfolio candidate", ["Work", "work", "Blue"]);
        Assert.True(store.SaveView(new SavedLibraryView("Best work", "candidate", MediaKind.Photo, true, null, "Work", "JPG", 365, 3)));

        var reloaded = new MediaLibraryStateStore(statePath);
        var annotation = reloaded.GetAnnotation("PHOTO.JPG");
        Assert.Equal("Portfolio candidate", annotation.Note);
        Assert.Equal(2, annotation.Tags.Count);
        Assert.Contains("Work", reloaded.Tags);
        var view = Assert.Single(reloaded.SavedViews);
        Assert.Equal("Best work", view.Name);
        Assert.True(reloaded.DeleteView("BEST WORK"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }
}
