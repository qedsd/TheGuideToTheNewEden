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
/// 频道监控页（对齐 WinUI 版 Views/Channel/ChannelMonitorPage）：
/// 左角色列表、中频道勾选列表、右监控设置（通知方式/声音/正则关键词）与命中的频道内容。
/// "频道内容"只显示命中关键词的消息（Important），最后一条加粗，超出 MaxShowItems 裁剪。
/// 页面缓存常驻，切走后监控继续运行。
/// </summary>
public partial class ChannelMonitorPage : Page
{
    private readonly ChannelMonitorViewModel _viewModel = new();

    private Paragraph? _lastParagraph;

    public ChannelMonitorPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.OnContentUpdate += OnContentUpdate;
    }

    // ---------- 角色与频道 ----------

    private void OnCharacterSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CharacterList.SelectedItem is ChannelMonitorItem item)
        {
            _viewModel.SelectedCharacter = item;
        }
    }

    private async void OnRefreshListClick(object sender, RoutedEventArgs e) => await _viewModel.RefreshListAsync();

    private void OnRefreshChannelsClick(object sender, RoutedEventArgs e) => _viewModel.RefreshChannelList();

    // ---------- 按钮 ----------

    private async void OnStartClick(object sender, RoutedEventArgs e) => await _viewModel.StartAsync();

    private void OnStopClick(object sender, RoutedEventArgs e) => _viewModel.StopSelected();

    private async void OnStartAllClick(object sender, RoutedEventArgs e) => await _viewModel.StartAllAsync();

    private void OnStopAllClick(object sender, RoutedEventArgs e) => _viewModel.StopAll();

    private void OnStopNotifyClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedCharacter is not null)
        {
            Services.ChannelIntel.ChannelMonitorNotifyService.Current.Stop(_viewModel.SelectedCharacter.Name);
        }
    }

    private void OnPickSoundFileClick(object sender, RoutedEventArgs e) => _viewModel.PickSoundFile();

    private void OnAddKeyClick(object sender, RoutedEventArgs e) => _viewModel.AddKey();

    private void OnDeleteKeyClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ChannelMonitorKey key)
        {
            _viewModel.DeleteKey(key);
        }
    }

    // ---------- 频道内容显示 ----------

    private void OnContentUpdate(object? sender, (string Name, IEnumerable<ChatContent> Contents) e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        _ = dispatcher.BeginInvoke(() =>
        {
            foreach (var content in e.Contents)
            {
                AppendContent(e.Name, content);
            }

            Trim();
            MsgContentsBox.ScrollToEnd();
        });
    }

    /// <summary>格式：&lt;角色&gt; [EVE时间] 发言人 &gt; 内容（最后一条加粗）。</summary>
    private void AppendContent(string name, ChatContent content)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
        var primary = GetResourceBrush("TextFillColorPrimaryBrush", Brushes.Black);
        var secondary = GetResourceBrush("TextFillColorSecondaryBrush", Brushes.Gray);
        paragraph.Inlines.Add(new Run($"{name} ") { Foreground = primary, FontWeight = FontWeights.SemiBold });
        paragraph.Inlines.Add(new Run($"[{content.EVETime:HH:mm:ss}] ") { Foreground = secondary, FontWeight = FontWeights.Light });
        paragraph.Inlines.Add(new Run($"{content.SpeakerName} > ") { Foreground = secondary });
        paragraph.Inlines.Add(new Run(content.Content) { Foreground = primary, FontWeight = FontWeights.Bold });

        if (_lastParagraph is not null)
        {
            foreach (var run in _lastParagraph.Inlines.OfType<Run>().Where(p => p.FontWeight == FontWeights.Bold))
            {
                run.FontWeight = FontWeights.Normal;
            }
        }

        _lastParagraph = paragraph;
        MsgContentsBox.Document.Blocks.Add(paragraph);
    }

    /// <summary>超出设置的最大显示条数时裁掉最早的段落。</summary>
    private void Trim()
    {
        var max = Math.Max(1, GameLogsSettingService.MaxShowItems);
        while (MsgContentsBox.Document.Blocks.Count > max && MsgContentsBox.Document.Blocks.FirstBlock is { } first)
        {
            MsgContentsBox.Document.Blocks.Remove(first);
        }
    }

    private void OnClearContentsClick(object sender, RoutedEventArgs e)
    {
        MsgContentsBox.Document.Blocks.Clear();
        _lastParagraph = null;
    }

    private static Brush GetResourceBrush(string key, Brush fallback)
        => Application.Current?.TryFindResource(key) as Brush ?? fallback;
}
