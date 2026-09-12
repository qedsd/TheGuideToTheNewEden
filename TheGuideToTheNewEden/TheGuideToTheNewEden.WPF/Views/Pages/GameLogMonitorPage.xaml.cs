using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.ViewModels.Channel;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 日志监控页（对齐 WinUI 版 Views/Channel/GameLogMonitorPage）：
/// 左角色列表、中配置页签（配置信息/监控模式/通知方式/监控关键词）、右实时日志。
/// 命中关键词的行标红、最新一条加粗，超出 <c>MaxShowItems</c> 裁剪并自动滚到底；
/// 切换角色时用该角色已积累的内容重绘。页面缓存常驻，切走后监控继续运行。
/// </summary>
public partial class GameLogMonitorPage : Page
{
    private readonly GameLogMonitorViewModel _viewModel = new();

    private Paragraph? _lastParagraph;

    public GameLogMonitorPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.OnContentUpdate += OnContentUpdate;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await _viewModel.InitAsync();

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameLogMonitorViewModel.SelectedGameLogInfo))
        {
            // 切换角色：用该角色已积累的内容重绘
            RebuildContents();
        }
    }

    // ---------- 角色列表 ----------

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await _viewModel.InitAsync();

    private void OnStartAllClick(object sender, RoutedEventArgs e) => _viewModel.StartAll();

    private void OnStopAllClick(object sender, RoutedEventArgs e) => _viewModel.StopAll();

    private void OnOpenLogFileClick(object sender, RoutedEventArgs e)
    {
        if (GetSelectedCharacter() is { } info)
        {
            try
            {
                Process.Start(new ProcessStartInfo(info.FilePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }
    }

    private void OnOpenLogFolderClick(object sender, RoutedEventArgs e)
    {
        if (GetSelectedCharacter() is { } info)
        {
            try
            {
                Process.Start("explorer.exe", $"/select,\"{info.FilePath}\"");
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }
    }

    /// <summary>右键菜单的 DataContext 是角色项；拿不到时回退到当前选中的角色。</summary>
    private GameLogInfo? GetSelectedCharacter()
        => CharacterList.SelectedItem as GameLogInfo ?? _viewModel.SelectedGameLogInfo;

    // ---------- 配置与监控 ----------

    /// <summary>新增配置。异常日志已隐藏，只剩"游戏日志"一种类型，故不再弹类型菜单。</summary>
    private void OnAddConfigClick(object sender, RoutedEventArgs e) => _viewModel.AddConfig(0);

    private void OnCloseConfigClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is GameLogItemConfig config)
        {
            _viewModel.RemoveConfig(config);
        }
    }

    private void OnStartClick(object sender, RoutedEventArgs e) => _viewModel.Start();

    private void OnStopClick(object sender, RoutedEventArgs e) => _viewModel.Stop();

    private void OnStopNotifyClick(object sender, RoutedEventArgs e) => _viewModel.StopNotify();

    private void OnAddKeyClick(object sender, RoutedEventArgs e) => _viewModel.AddKey();

    private void OnDeleteKeyClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is GameLogMonityKey key)
        {
            _viewModel.DeleteKey(key);
        }
    }

    private void OnPickSoundFileClick(object sender, RoutedEventArgs e) => _viewModel.PickSoundFile();

    /// <summary>配置里的文本类字段在失焦后立即持久化（绑定本身不触发保存）。</summary>
    private void OnConfigFieldLostFocus(object sender, RoutedEventArgs e) => _viewModel.SaveSetting();

    // ---------- 实时日志显示 ----------

    private void OnContentUpdate(GameLogItem item, IEnumerable<GameLogContent> news)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            // 只显示当前选中角色的日志
            if (_viewModel.SelectedGameLogInfo?.ListenerID != item.Info.ListenerID)
            {
                return;
            }

            AppendContents(news);
        });
    }

    private void RebuildContents()
    {
        LogContentsBox.Document.Blocks.Clear();
        _lastParagraph = null;

        var contents = _viewModel.SelectedGameLogInfo?.LogContents;
        if (contents is { Count: > 0 })
        {
            AppendContents(contents);
        }
    }

    private void AppendContents(IEnumerable<GameLogContent> news)
    {
        foreach (var content in news)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
            paragraph.Inlines.Add(new Run(content.SourceContent)
            {
                Foreground = content.Important
                    ? GetResourceBrush("SystemFillColorCriticalBrush", Brushes.OrangeRed)
                    : GetResourceBrush("TextFillColorPrimaryBrush", Brushes.Black),
            });
            LogContentsBox.Document.Blocks.Add(paragraph);
            _lastParagraph = paragraph;
        }

        // 最新一条加粗（先清掉上一条的加粗）
        if (_lastParagraph is not null)
        {
            var blocks = LogContentsBox.Document.Blocks;
            if (blocks.Count > 1 && blocks.ElementAt(blocks.Count - 2) is Paragraph previous)
            {
                foreach (var run in previous.Inlines.OfType<Run>())
                {
                    run.FontWeight = FontWeights.Normal;
                }
            }

            foreach (var run in _lastParagraph.Inlines.OfType<Run>())
            {
                run.FontWeight = FontWeights.Bold;
            }
        }

        Trim();
        LogContentsBox.ScrollToEnd();
    }

    private void Trim()
    {
        var max = Math.Max(1, GameLogsSettingService.MaxShowItems);
        while (LogContentsBox.Document.Blocks.Count > max && LogContentsBox.Document.Blocks.FirstBlock is { } first)
        {
            LogContentsBox.Document.Blocks.Remove(first);
        }
    }

    private void OnClearContentsClick(object sender, RoutedEventArgs e)
    {
        LogContentsBox.Document.Blocks.Clear();
        _lastParagraph = null;
        _viewModel.SelectedGameLogInfo?.LogContents.Clear();
    }

    private static Brush GetResourceBrush(string key, Brush fallback)
        => Application.Current?.TryFindResource(key) as Brush ?? fallback;
}
