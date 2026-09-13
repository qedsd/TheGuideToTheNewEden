using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WPF.Services.GamePreview;
using TheGuideToTheNewEden.WPF.ViewModels.GamePreview;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 多开页面（对齐 WinUI 版 GamePreviewMgrPage，但已移除 IPC 预览模式）：
/// 左进程列表 / 中设置（选中项 · 全局）/ 右选中进程实时预览。
/// <para>
/// 预览窗口由 <see cref="PreviewWindowManager"/> 独立持有，本页只负责展示与转发操作——
/// 因此切到别的页面时预览窗口与全局快捷键继续工作。页面被导航缓存，实例只创建一次。
/// </para>
/// </summary>
public partial class GamePreviewPage : Page
{
    /// <summary>源画面比例未知时（未选中进程）预览区的默认比例。</summary>
    private const double DefaultPreviewAspect = 16.0 / 9.0;

    private readonly GamePreviewViewModel _viewModel = new();
    private readonly SelectionPreview _selectionPreview = new();

    private bool _initialized;
    private bool _exitHooked;
    private PreviewItem? _subscribedItem;

    public GamePreviewPage()
    {
        InitializeComponent();
        DataContext = _viewModel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.Setting.PropertyChanged += OnGlobalSettingPropertyChanged;
        PreviewArea.SizeChanged += (_, _) => UpdatePreviewLayout();
    }

    // ---------- 生命周期 ----------

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_exitHooked)
        {
            _exitHooked = true;
            Application.Current.Exit += OnApplicationExit;
        }

        if (Window.GetWindow(this) is { } owner)
        {
            _selectionPreview.Attach(PreviewHost, owner);
            _selectionPreview.StateChanged += OnSelectionPreviewStateChanged;
            UpdateSelectionPreviewSource();
            UpdatePreviewLayout();
        }

        if (!_initialized)
        {
            _initialized = true;
            await _viewModel.InitializeAsync();
            _viewModel.FillOrderFromProcesses();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // 缩略图是直接画在主窗口上的，离开页面必须注销，否则那块区域会留下游戏画面
        _selectionPreview.StateChanged -= OnSelectionPreviewStateChanged;
        _selectionPreview.Detach();
    }

    private void OnApplicationExit(object? sender, ExitEventArgs e)
    {
        _selectionPreview.Dispose();
        _viewModel.Dispose();
    }

    private void OnSelectionPreviewStateChanged() => UpdatePreviewLayout();

    /// <summary>
    /// 预览区"宽度占满、高度按源画面比例"：这样画面不会被拉伸，也不会出现黑边。
    /// 高度夹在卡片可用高度内，超出时由缩略图按比例在内部留边（不会溢出被裁）。
    /// </summary>
    private void UpdatePreviewLayout()
    {
        var aspect = _selectionPreview.SourceAspect > 0 ? _selectionPreview.SourceAspect : DefaultPreviewAspect;
        var width = PreviewArea.ActualWidth;
        var availableHeight = PreviewArea.ActualHeight;

        if (width > 0 && aspect > 0)
        {
            var height = width / aspect;
            if (availableHeight > 0)
            {
                height = Math.Min(height, availableHeight);
            }

            PreviewHost.Height = height;
        }

        // 没有可用画面（未选中 / 客户端已退出 / 客户端已最小化）时显示占位提示。
        // "已最小化"由 SelectionPreview 归入"没有画面"，文案沿用既有键（其原文已含"已最小化"）。
        if (_selectionPreview.IsSourceAvailable)
        {
            PreviewHint.Visibility = Visibility.Collapsed;
        }
        else
        {
            var hasSelection = _viewModel.SelectedProcess is not null;
            PreviewHint.Visibility = Visibility.Visible;
            PreviewHint.SetResourceReference(
                TextBlock.TextProperty,
                hasSelection ? "GamePreviewPage_PreviewUnavailable" : "GamePreviewPage_PreviewTip");
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GamePreviewViewModel.SelectedProcess)
            or nameof(GamePreviewViewModel.SelectedSetting))
        {
            SubscribeSelectedItem();
            UpdateSelectionPreviewSource();
            UpdatePreviewLayout();
        }
    }

    /// <summary>选中项的设置改动 → 立即套用到预览窗口并保存。</summary>
    private void SubscribeSelectedItem()
    {
        if (ReferenceEquals(_subscribedItem, _viewModel.SelectedSetting))
        {
            return;
        }

        if (_subscribedItem is not null)
        {
            _subscribedItem.PropertyChanged -= OnSelectedItemPropertyChanged;
        }

        _subscribedItem = _viewModel.SelectedSetting;
        if (_subscribedItem is not null)
        {
            _subscribedItem.PropertyChanged += OnSelectedItemPropertyChanged;
        }
    }

    private void OnSelectedItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => _viewModel.NotifySettingChanged(e.PropertyName == nameof(PreviewItem.HotKey));

    /// <summary>全局设置改动 → 保存（快捷键由 VM 的属性自行处理重新注册）。</summary>
    private void OnGlobalSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => _viewModel.Save();

    private void UpdateSelectionPreviewSource()
        => _selectionPreview.SetSource(_viewModel.SelectedProcess?.MainWindowHandle ?? IntPtr.Zero);

    // ---------- 进程列表 ----------

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshAsync();
        _viewModel.FillOrderFromProcesses();
    }

    private void OnStartAllClick(object sender, RoutedEventArgs e) => _viewModel.StartAll();

    private void OnStopAllClick(object sender, RoutedEventArgs e) => _viewModel.StopAll();

    /// <summary>右键菜单的 DataContext 不一定是当前选中项，先把它设为选中项再操作。</summary>
    private bool SelectFromSender(object sender)
    {
        if ((sender as FrameworkElement)?.DataContext is ProcessInfo process)
        {
            _viewModel.SelectedProcess = process;
            return true;
        }

        return _viewModel.SelectedProcess is not null;
    }

    private void OnStartProcessClick(object sender, RoutedEventArgs e)
    {
        if (SelectFromSender(sender))
        {
            _viewModel.StartSelected();
        }
    }

    private void OnStopProcessClick(object sender, RoutedEventArgs e)
    {
        if (SelectFromSender(sender))
        {
            _viewModel.StopSelected();
        }
    }

    private void OnMoveUpClick(object sender, RoutedEventArgs e)
    {
        if (SelectFromSender(sender))
        {
            _viewModel.MoveSelected(-1);
            _viewModel.FillOrderFromProcesses();
        }
    }

    private void OnMoveDownClick(object sender, RoutedEventArgs e)
    {
        if (SelectFromSender(sender))
        {
            _viewModel.MoveSelected(1);
            _viewModel.FillOrderFromProcesses();
        }
    }

    private void OnActivateSourceClick(object sender, RoutedEventArgs e)
    {
        if (SelectFromSender(sender) && _viewModel.SelectedProcess is { } process)
        {
            GameClientService.Activate(process.MainWindowHandle, _viewModel.Setting.SetForegroundWindowMode);
        }
    }

    // ---------- 启停 / 批量 ----------

    private void OnStartClick(object sender, RoutedEventArgs e) => _viewModel.StartSelected();

    private void OnStopClick(object sender, RoutedEventArgs e) => _viewModel.StopSelected();

    private void OnApplyUniformSizeClick(object sender, RoutedEventArgs e) => _viewModel.ApplyUniformSize();

    private void OnApplyAutoLayoutClick(object sender, RoutedEventArgs e) => _viewModel.ApplyAutoLayout();

    private void OnRestorePositionClick(object sender, RoutedEventArgs e)
    {
        if (SelectFromSender(sender))
        {
            _viewModel.RestorePosition();
        }
    }

    private void OnApplyToAllClick(object sender, RoutedEventArgs e) => _viewModel.ApplySelectedToAll();

    private void OnItemHotkeyLostFocus(object sender, RoutedEventArgs e) => _viewModel.NotifySettingChanged(true);

    // ---------- 分组 ----------

    private void OnAddGroupClick(object sender, RoutedEventArgs e) => _viewModel.AddGroup();

    private void OnRemoveGroupClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is PreviewHotKeyGroup group)
        {
            _viewModel.RemoveGroup(group);
        }
    }

    private void OnApplyGroupClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is PreviewHotKeyGroup group)
        {
            _viewModel.UpdateGroup(group);
        }
    }

    // ---------- 顺序 ----------

    private void OnFillOrderClick(object sender, RoutedEventArgs e) => _viewModel.FillOrderFromProcesses();

    private void OnApplyOrderClick(object sender, RoutedEventArgs e) => _viewModel.ApplyOrderText();
}
