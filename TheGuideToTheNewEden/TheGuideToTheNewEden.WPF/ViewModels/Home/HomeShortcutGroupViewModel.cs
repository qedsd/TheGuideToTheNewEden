using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace TheGuideToTheNewEden.WPF.ViewModels.Home;

/// <summary>
/// 分组模式下的一段磁贴（P3「分段显示」）：一个小标题 + 该分组下的磁贴。
/// 分组标题取功能目录里的分组键（<c>Nav.*</c>），因此与左侧导航的分组名一致。
/// </summary>
public sealed class HomeShortcutGroupViewModel : INotifyPropertyChanged
{
    public HomeShortcutGroupViewModel(string groupKey, IEnumerable<HomeShortcutTileViewModel> tiles)
    {
        GroupKey = groupKey;
        Tiles = new ObservableCollection<HomeShortcutTileViewModel>(tiles);
    }

    public string GroupKey { get; }

    public ObservableCollection<HomeShortcutTileViewModel> Tiles { get; }

    /// <summary>分组标题（现取资源，语言切换后 <see cref="Refresh"/> 即可）。</summary>
    public string Title => Application.Current?.TryFindResource(GroupKey) as string ?? GroupKey;

    /// <summary>「N 个」。</summary>
    public string CountText => string.Format(
        Application.Current?.TryFindResource("Home.Quick.GroupCount") as string ?? "{0}", Tiles.Count);

    public void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(CountText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
