using WinLive.Core;
using Xunit;

namespace WinLive.Tests;

public sealed class MediaDuplicateFinderTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "WinLive.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Finds_only_byte_identical_files_of_the_same_size()
    {
        Directory.CreateDirectory(_folder);
        var first = Create("first.jpg", "same bytes");
        var second = Create("second.jpg", "same bytes");
        var different = Create("different.jpg", "other data");
        var items = new[] { Item(first), Item(second), Item(different) };

        var groups = await new MediaDuplicateFinder().FindAsync(items, null, CancellationToken.None);

        var duplicate = Assert.Single(groups);
        Assert.Equal(2, duplicate.Items.Count);
        Assert.DoesNotContain(duplicate.Items, item => item.PrimaryPath == different);
    }

    private string Create(string name, string content)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static MediaItem Item(string path) => new(path, MediaKind.Photo, path, null, DateTimeOffset.Now, null, DynamicRange.Standard);

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }
}
