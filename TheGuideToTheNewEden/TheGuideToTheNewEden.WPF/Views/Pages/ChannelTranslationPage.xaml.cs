using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels.Channel;
using TheGuideToTheNewEden.WPF.Views.UserControls;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 频道翻译页：勾选频道后把新消息交给 <see cref="Services.Translation.ChatTranslationEngine"/> 串行翻译
/// （AI 源 + 本地 SDE 术语约束，聊天标记先保护后还原），右侧实时显示原文与译文。
/// 「弹窗」用 <see cref="ToolWindow"/> 承载同一份结果视图，与页面共享同一个 ViewModel。
/// </summary>
public partial class ChannelTranslationPage : Page
{
    private readonly ChannelTranslationViewModel _viewModel = new();
    private ToolWindow? _window;

    public ChannelTranslationPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // 页面实例常驻，离开时关掉可能开着的弹窗（它的 DataContext 就是本页 VM）
        if (_window is not null)
        {
            _window.AllowClose();
            _window.Close();
            _window = null;
        }
    }

    private void OnCharacterSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: Core.Models.ChannelIntel.ChannelIntelListener listener })
        {
            _viewModel.SelectedCharacter = listener;
        }
    }

    private void OnStartClick(object sender, RoutedEventArgs e) => _viewModel.StartSelected();

    private void OnStopClick(object sender, RoutedEventArgs e) => _viewModel.StopSelected();

    private void OnStartAllClick(object sender, RoutedEventArgs e) => _viewModel.StartAll();

    private void OnStopAllClick(object sender, RoutedEventArgs e) => _viewModel.StopAll();

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await _viewModel.RefreshAsync();

    private void OnClearClick(object sender, RoutedEventArgs e) => _viewModel.ClearItems();

    /// <summary>未配置引导里的「去配置」（打开 设置 → AI 翻译）。</summary>
    private void OnGoConfigureClick(object sender, RoutedEventArgs e) => _viewModel.OpenAiSettings();

    /// <summary>未配置引导里的「重新检测」。</summary>
    private void OnRecheckClick(object sender, RoutedEventArgs e) => _viewModel.RecheckConfiguration();

    private void OnPopWindowClick(object sender, RoutedEventArgs e)
    {
        if (_window is null)
        {
            _window = new ToolWindow(
                new ChannelTranslationResultView { DataContext = _viewModel },
                ToolWindowTitleStyle.Default,
                showTopmostButton: true,
                showInTaskbar: true,
                width: 760,
                height: 700)
            {
                DisplayTitle = FindString("Nav.ChannelTranslation"),
            };
            _window.Closed += (_, _) => _window = null;
            CenterOverHost(_window);
        }

        _window.Show();
        _window.Activate();
    }

    /// <summary>居中到宿主主窗口（不设 Owner：未置顶时就是普通窗口，与翻译页弹窗一致）。</summary>
    private void CenterOverHost(Window window)
    {
        var host = Window.GetWindow(this);
        if (host is null)
        {
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        var workArea = SystemParameters.WorkArea;
        var left = host.Left + (host.ActualWidth - window.Width) / 2;
        var top = host.Top + (host.ActualHeight - window.Height) / 2;
        window.Left = Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - window.Width));
        window.Top = Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - window.Height));
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
