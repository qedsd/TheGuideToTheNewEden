using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Map;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Map;

/// <summary>
/// 主权分组编辑（对齐 WinUI <c>MapDataTypeControl</c> 的分组表 + 重置）：
/// 分组号决定星图配色，同一分组号的联盟同色；保存后写 <c>Configs/SOVGroup.json</c>。
/// </summary>
public partial class SovGroupSettingView : UserControl
{
    public SovGroupSettingView()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += async (_, _) => await ReloadAsync(false);
    }

    public ObservableCollection<SovInfo> Groups { get; } = [];

    /// <summary>分组已保存（星图页据此重新着色）。</summary>
    public event EventHandler? GroupsSaved;

    private async Task ReloadAsync(bool forceRefresh)
    {
        var infos = await SovService.LoadAsync(forceRefresh);
        Groups.Clear();
        foreach (var info in infos)
        {
            Groups.Add(info);
        }

        StatusText.Text = string.Format(
            Application.Current?.TryFindResource("MapTool_Sov_Status") as string ?? "{0}",
            Groups.Count);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SovService.SaveGroups(Groups);
        GroupsSaved?.Invoke(this, EventArgs.Empty);
        StatusText.Text = Application.Current?.TryFindResource("MapTool_Sov_Saved") as string ?? "Saved";
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        SovService.ResetGroupsToDefault([.. Groups]);
        // 重建列表以刷新 NumberBox（SovInfo 不实现 INPC）
        var items = Groups.ToList();
        Groups.Clear();
        foreach (var item in items)
        {
            Groups.Add(item);
        }

        SovService.SaveGroups(Groups);
        GroupsSaved?.Invoke(this, EventArgs.Empty);
        StatusText.Text = Application.Current?.TryFindResource("MapTool_Sov_ResetDone") as string ?? "Reset";
    }

    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        SovService.ClearCache();
        await ReloadAsync(true);
        GroupsSaved?.Invoke(this, EventArgs.Empty);
    }
}
