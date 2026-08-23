using System.Collections.ObjectModel;
using System.Collections;
using WinLive.Core;

namespace WinLive.ViewModels;

public sealed class MediaGroupViewModel(MediaGroup group) : IEnumerable<MediaTileViewModel>
{
    public string Title { get; } = group.Title;
    public ObservableCollection<MediaTileViewModel> Items { get; } = new(group.Items.Select(item => new MediaTileViewModel(item)));

    public IEnumerator<MediaTileViewModel> GetEnumerator() => Items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
