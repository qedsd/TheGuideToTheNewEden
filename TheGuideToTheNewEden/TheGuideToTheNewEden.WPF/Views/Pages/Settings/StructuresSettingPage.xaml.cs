using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.Models.Universe;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Settings;

/// <summary>
/// 玩家建筑（市场结构）管理：本地列表的增删。
/// 通过 ESI 按 ID / 按角色查询结构需要账号授权，授权流程尚未移植，故此处给出提示。
/// </summary>
public partial class StructuresSettingPage : Page
{
    public StructuresSettingPage()
    {
        InitializeComponent();

        AddedGrid.ItemsSource = StructureService.GetMarketStrutures();
        RemoveButton.Click += (_, _) => RemoveSelected();
        AddByIdButton.Click += (_, _) => AddById();
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

    private void AddById()
    {
        if (!long.TryParse(StructureIdBox.Text?.Trim(), out var id) || id <= 0)
        {
            ShowNotice(Application.Current.TryFindResource("Settings.Structures.AddById") as string ?? "ID");
            return;
        }

        // 结构名称等信息需经 ESI 查询（需授权），此处仅登记 ID。
        StructureService.Add(id, null);
        AddedGrid.Items.Refresh();
        StructureIdBox.Text = string.Empty;
    }

    private void ShowNotice(string title)
    {
        System.Windows.MessageBox.Show(
            Application.Current.TryFindResource("Settings.Structures.SearchUnavailable") as string ?? string.Empty,
            title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }
}