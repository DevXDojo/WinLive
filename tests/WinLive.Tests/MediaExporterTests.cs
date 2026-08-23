using WinLive.Core;
using Xunit;

namespace WinLive.Tests;

public sealed class MediaExporterTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "WinLive.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Copies_live_pair_and_renames_collisions_without_changing_sources()
    {
        var source = Path.Combine(_folder, "source");
        var destination = Path.Combine(_folder, "destination");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        var still = Path.Combine(source, "IMG_1.HEIC");
        var motion = Path.Combine(source, "IMG_1.MOV");
        File.WriteAllText(still, "still");
        File.WriteAllText(motion, "motion");
        File.WriteAllText(Path.Combine(destination, "IMG_1.HEIC"), "existing");
        var item = new MediaItem("1", MediaKind.LivePhoto, still, motion, DateTimeOffset.Now, TimeSpan.FromSeconds(2), DynamicRange.Standard);

        var result = await new MediaExporter().ExportAsync([item], destination, null, CancellationToken.None);

        Assert.Equal(1, result.MediaItems);
        Assert.Equal(2, result.Files);
        Assert.Equal("still", File.ReadAllText(still));
        Assert.Equal("still", File.ReadAllText(Path.Combine(destination, "IMG_1 (2).HEIC")));
        Assert.Equal("motion", File.ReadAllText(Path.Combine(destination, "IMG_1.MOV")));
        Assert.Equal("existing", File.ReadAllText(Path.Combine(destination, "IMG_1.HEIC")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }
}
