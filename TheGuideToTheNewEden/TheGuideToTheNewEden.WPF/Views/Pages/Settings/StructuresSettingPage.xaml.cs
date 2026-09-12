using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.Models.Universe;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Settings;

/// <summary>
/// 玩家建筑（市场结构）管理：本地列表的增删。
/// 按结构 ID 添加时会用默认角色的授权经 ESI 解析名称与所在星系/星域（需
/// <c>esi-universe.read_structures.v1</c> 权限）；已缓存过的结构直接命中本地缓存。
/// </summary>
public partial class StructuresSettingPage : Page
{
    public StructuresSettingPage()
    {
        InitializeComponent();

        AddedGrid.ItemsSource = StructureService.GetMarketStrutures();
        RemoveButton.Click += (_, _) => RemoveSelected();
        AddByIdButton.Click += async (_, _) => await AddByIdAsync();
    }

    private void RemoveSelected()
    {
        var selected = AddedGrid.SelectedItems.OfType<Structure>().ToList();
        if (selected.Count == 0)
        {
            return;
        }

        StructureService.Remove(selected);
        AddedGrid.Items.Refresh();
    }

    private async Task AddByIdAsync()
    {
        if (!long.TryParse(StructureIdBox.Text?.Trim(), out var id) || id <= 0)
        {
            ShowNotice(FindString("Settings.Structures.AddById"));
            return;
        }

        if (StructureService.GetMarketStrutures().Any(p => p.Id == id))
        {
            return;
        }

        AddByIdButton.IsEnabled = false;
        try
        {
            // 先命中本地缓存（Structures.json），未命中则用默认角色经 ESI 解析
            var structure = await StructureService.QueryStructureAsync(id);
            if (structure is null)
            {
                ShowNotice(FindString("Settings.Structures.ResolveFailed"));
                return;
            }

            StructureService.Add(structure);
            AddedGrid.Items.Refresh();
            StructureIdBox.Text = string.Empty;
        }
        finally
        {
            AddByIdButton.IsEnabled = true;
        }
    }

    private void ShowNotice(string title)
    {
        MessageBox.Show(
            FindString("Settings.Structures.SearchUnavailable"),
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
