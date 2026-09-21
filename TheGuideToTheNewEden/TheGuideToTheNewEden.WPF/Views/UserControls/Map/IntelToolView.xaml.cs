using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels.Map;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Map;

/// <summary>
/// 情报工具窗口（对齐 WinUI <c>Tools/IntelTool</c> 的独立工具窗形态）：
/// 实时数据 / 过滤（排除项·包含项列表增删）/ 设置（ZKB 与频道时长、图标与消息上限、清怪模式），
/// 底部是监听开关与清空。DataContext 是星图页的 <see cref="MapPageViewModel"/>——与主图共用同一份状态，
/// 主图负责画红圈与舰船图标。
/// 实时数据行是"图形化卡片"：时间 + 相对时间 / 联盟徽标 / 星系 / 星域 / 受害方（舰船·角色·势力）/ 攻方（舰船 ×N · 角色 · 势力）/ 来源 / 内容，
/// 图标与名称可点击（定位主图或打开 KB 击杀详情 / 实体页）。
/// </summary>
public partial class IntelToolView : UserControl
{
    private readonly MapPageViewModel _viewModel;
    private readonly Action<int>? _locateSystem;
    private readonly Action<int>? _locateRegion;

    public IntelToolView(MapPageViewModel viewModel, Action<int>? locateSystem = null, Action<int>? locateRegion = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _locateSystem = locateSystem;
        _locateRegion = locateRegion;
        DataContext = viewModel;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.IntelMessages.CollectionChanged += Messages_CollectionChanged;
        Loaded += IntelToolView_Loaded;
        Unloaded += IntelToolView_Unloaded;
        RefreshStatus();
    }

    private void IntelToolView_Loaded(object sender, RoutedEventArgs e) => _viewModel.LoadIntelFilters();

    private void IntelToolView_Unloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.IntelMessages.CollectionChanged -= Messages_CollectionChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MapPageViewModel.IntelRunning) or nameof(MapPageViewModel.ActiveListeners) or nameof(MapPageViewModel.IntelHintText))
        {
            RefreshStatus();
        }
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshStatus();

    private void RefreshStatus()
    {
        StartStopButton.Content = FindString(_viewModel.IntelRunning ? "IntelTool_Stop" : "IntelTool_Start");
        StatusText.Text = _viewModel.ActiveListeners.Count > 0
            ? string.Format(FindString("IntelTool_Status"), string.Join(" / ", _viewModel.ActiveListeners))
            : FindString("MapPage_IntelNoSessions");
        CountText.Text = string.Format(FindString("IntelTool_Count"), _viewModel.IntelMessages.Count);
    }

    private void StartStop_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IntelRunning)
        {
            _viewModel.SaveIntelConfig();
            _viewModel.StopIntel();
        }
        else
        {
            _viewModel.StartIntel();
        }

        RefreshStatus();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearIntelMessages();
        RefreshStatus();
    }

    private void MessageGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid { SelectedItem: IntelMsgItem item })
        {
            MessageGrid.SelectedItem = null;
            if (item.SystemId > 0)
            {
                _locateSystem?.Invoke(item.SystemId);
            }
        }
    }

    /// <summary>行右键 = 回主图并定位该星系（与 WinUI 一致）。</summary>
    private void MessageGrid_RightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (MessageGrid.SelectedItem is IntelMsgItem { SystemId: > 0 } item)
        {
            _locateSystem?.Invoke(item.SystemId);
        }
    }

    // ---------- 行内图标 / 名称的点击 ----------

    /// <summary>点图标时不要顺带把整行选中（否则会连带触发一次"定位到该星系"）。</summary>
    private void Icon_PreviewMouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

    /// <summary>舰船图标（受害方与攻方）→ 打开该击杀的 KB 详情。</summary>
    private void Killmail_Click(object sender, MouseButtonEventArgs e)
    {
        if (FindMessage(sender) is { KillmailId: > 0 } message)
        {
            KbNavigation.OpenKillmail(message.KillmailId);
        }
    }

    private void VictimCharacter_Click(object sender, MouseButtonEventArgs e)
    {
        if (FindMessage(sender) is { VictimCharacterId: > 0 } message)
        {
            KbNavigation.OpenEntity((int)message.VictimCharacterId, IdName.CategoryEnum.Character, message.VictimCharacterName);
        }
    }

    private void VictimFaction_Click(object sender, MouseButtonEventArgs e)
    {
        if (FindMessage(sender) is { VictimFactionId: > 0 } message)
        {
            KbNavigation.OpenEntity((int)message.VictimFactionId, FactionCategory(message.VictimFactionIsAlliance), message.VictimFactionName);
        }
    }

    private void AttackerCharacter_Click(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is IntelShipBadge { CharacterId: > 0 } badge)
        {
            KbNavigation.OpenEntity((int)badge.CharacterId, IdName.CategoryEnum.Character);
        }
    }

    private void AttackerFaction_Click(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is IntelShipBadge { FactionId: > 0 } badge)
        {
            KbNavigation.OpenEntity((int)badge.FactionId, FactionCategory(badge.FactionIsAlliance));
        }
    }

    private void SystemText_Click(object sender, MouseButtonEventArgs e)
    {
        if (FindMessage(sender) is { SystemId: > 0 } message)
        {
            _locateSystem?.Invoke(message.SystemId);
        }
    }

    private void RegionText_Click(object sender, MouseButtonEventArgs e)
    {
        if (FindMessage(sender) is { RegionId: > 0 } message)
        {
            _locateRegion?.Invoke(message.RegionId);
        }
    }

    private static IdName.CategoryEnum FactionCategory(bool isAlliance) =>
        isAlliance ? IdName.CategoryEnum.Alliance : IdName.CategoryEnum.Corporation;

    /// <summary>从被点元素向上找到所属的 DataGrid 行，取回该行情报（攻方徽标在嵌套 ItemsControl 里，不能只看 DataContext）。</summary>
    private static IntelMsgItem? FindMessage(object sender)
    {
        if (sender is not DependencyObject element)
        {
            return null;
        }

        while (element is not null)
        {
            if (element is DataGridRow row)
            {
                return row.Item as IntelMsgItem;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private void ExclusionSearch_ItemSelected(IdName entity) => _viewModel.AddEntityExclusion(entity);

    private void InclusionSearch_ItemSelected(IdName entity) => _viewModel.AddEntityInclusion(entity);

    private void RemoveExclusion_Click(object sender, RoutedEventArgs e)
    {
        foreach (var entity in ExclusionList.SelectedItems.Cast<IdName>().ToList())
        {
            _viewModel.RemoveEntityExclusion(entity);
        }
    }

    private void RemoveInclusion_Click(object sender, RoutedEventArgs e)
    {
        foreach (var entity in InclusionList.SelectedItems.Cast<IdName>().ToList())
        {
            _viewModel.RemoveEntityInclusion(entity);
        }
    }

    /// <summary>刷新可选频道（频道预警里已启动的会话）。</summary>
    private void RefreshChannels_Click(object sender, RoutedEventArgs e) => _viewModel.RefreshChannels();

    /// <summary>频道勾选变化 → 立即生效（运行中会重新订阅推送）。</summary>
    private void ChannelCheck_Click(object sender, RoutedEventArgs e) => _viewModel.ApplyChannelSelection();

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
