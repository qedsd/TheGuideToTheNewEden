using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.ViewModels.Business;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>倒货分析子视图：左侧设置/右侧结果。详情窗口在此弹出（VM 不接触窗口）。</summary>
public partial class ScalperAnalyseView : UserControl
{
    private ScalperItemDetailWindow? _detailWindow;

    public ScalperAnalyseView()
    {
        InitializeComponent();

        // 阶段 286：选择树（复用目标物品那套）勾选变化 → 同步到排除列表
        FilterTypeTree.SelectedTypesChanged += OnFilterTypeSelectionChanged;

        // 阶段 290/293：右键菜单自己接管，并预建（避免首次打开时加载模板/主题造成卡顿）
    }

    private void OnCancelBusyClick(object sender, RoutedEventArgs e) => Vm?.Cancel();

    /// <summary>阶段 299：推荐权重预设（Tag = conservative / balanced / aggressive）。</summary>
    private void OnWeightPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string kind } && Vm is { } vm)
        {
            vm.ApplyWeightPreset(kind);
        }
    }
    private void OnFilterTypeSelectionChanged(IReadOnlyList<int> ids)
    {
        if (Vm is { } vm)
        {
            vm.SyncFilterTypesFromSelection(ids);
        }
    }

    private ScalperPageViewModel? Vm => DataContext as ScalperPageViewModel;

    private async void OnGetOrdersClick(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm)
        {
            await vm.GetOrdersAsync();
        }
    }

    private async void OnAnalyseClick(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm)
        {
            await vm.AnalyseAsync();
        }
    }

    private void OnRemoveFilterTypesClick(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm)
        {
            vm.RemoveFilterTypes(FilterTypeList.SelectedItems.Cast<InvType>().ToList());
        }
    }

    /// <summary>阶段 300/301：右键打开倒货物品的市场详情。</summary>
    private void OnOpenDetailClick(object sender, RoutedEventArgs e)
    {
        if (ResultGrid.SelectedItem is ScalperItem item)
        {
            OpenDetail(item);
        }
    }

    private void OnAddToCartClick(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm)
        {
            return;
        }

        var items = ResultGrid.SelectedItems.Cast<ScalperItem>().ToList();
        if (items.Count == 0)
        {
            return;
        }

        vm.AddToCart(items);
    }


    private void OnResultGridDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ResultGrid.SelectedItem is ScalperItem item)
        {
            OpenDetail(item);
        }
    }

    private void OpenDetail(ScalperItem item)
    {
        if (_detailWindow is null)
        {
            _detailWindow = new ScalperItemDetailWindow();
            _detailWindow.Closed += (_, _) => _detailWindow = null;
        }

        _detailWindow.SetItem(item);
        // 阶段 301 修复：从未 Show 过的窗口 Activate 无效——首次双击"没反应"的根因
        _detailWindow.Show();
        _detailWindow.Activate();
    }

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;
}
