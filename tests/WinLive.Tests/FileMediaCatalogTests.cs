using WinLive.Core;
using Xunit;

namespace WinLive.Tests;

public sealed class FileMediaCatalogTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "WinLive.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Pairs_same_named_heic_and_mov_and_hides_motion_file()
    {
        Create("IMG_0001.HEIC");
        Create("IMG_0001.MOV");
        Create("clip.MOV");

        var groups = await new FileMediaCatalog(new TestDecoder()).ScanAsync(_folder, CancellationToken.None);
        var items = groups.SelectMany(group => group.Items).ToArray();

        var live = Assert.Single(items, item => item.Kind == MediaKind.LivePhoto);
        Assert.EndsWith("IMG_0001.HEIC", live.PrimaryPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("IMG_0001.MOV", live.MotionPath, StringComparison.OrdinalIgnoreCase);
        Assert.Single(items, item => item.Kind == MediaKind.Video);
    }

    [Fact]
    public async Task Recursively_finds_supported_files_and_ignores_others()
    {
        Create("nested/photo.jpg");
        Create("nested/clip.mkv");
        Create("nested/readme.txt");

        var groups = await new FileMediaCatalog(new TestDecoder()).ScanAsync(_folder, CancellationToken.None);

        Assert.Equal(2, groups.SelectMany(group => group.Items).Count());
    }

    [Fact]
    public async Task Honors_an_already_cancelled_scan()
    {
        Create("photo.png");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FileMediaCatalog(new TestDecoder()).ScanAsync(_folder, cancellation.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private void Create(string relativePath)
    {
        var path = Path.Combine(_folder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "fixture");
    }

    private sealed class TestDecoder : IMediaDecoder
    {
        public bool CanDecode(string path) => true;
        public DynamicRange DetectDynamicRange(string path) => DynamicRange.Standard;
        public Task<TimeSpan?> GetDurationAsync(string path, CancellationToken cancellationToken) => Task.FromResult<TimeSpan?>(TimeSpan.FromSeconds(1));
    }
}
