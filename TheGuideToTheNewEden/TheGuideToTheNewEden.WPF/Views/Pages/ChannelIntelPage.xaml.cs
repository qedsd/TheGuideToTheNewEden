using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.ViewModels.Channel;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 频道预警页（对齐 WinUI 版 Views/Channel/ChannelIntelPage）：
/// 左角色列表、中频道勾选列表、右预警设置与频道内容。
/// "频道内容"用只读 RichTextBox 按预警类型着色（预警=危险色并加粗舰船名、解除=成功色）。
/// 页面缓存常驻，切走后监控继续运行；应用退出时统一释放（App.OnExit）。
/// </summary>
public partial class ChannelIntelPage : Page
{
    private readonly ChannelIntelViewModel _viewModel = new();

    private sealed class NameDbItem
    {
        public string Path { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;
    }

    public ChannelIntelPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ChannelIntelViewModel.Session))
            {
                OnSessionChanged();
            }
        };
        _viewModel.ChatContents.CollectionChanged += ChatContents_CollectionChanged;
        _viewModel.ZKBIntelContents.CollectionChanged += ZKBIntelContents_CollectionChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ShipNameCacheService.Current.Init(); // 舰船名匹配缓存（预警内容加粗用）
    }

    // ---------- 角色与频道 ----------

    private void OnCharacterSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CharacterList.SelectedItem is ChannelIntelListener character)
        {
            _viewModel.SelectedCharacter = character;
        }
    }

    private void OnSessionChanged()
    {
        // 初始化星系搜索列表与语言库多选
        var systems = _viewModel.MapSolarSystems;
        SystemList.ItemsSource = systems;
        ApplySystemFilter(SystemSearchBox.Text);
        NameDbsList.ItemsSource = _viewModel.NameDbs.Select(p => new NameDbItem
        {
            Path = p,
            Name = p == _viewModel.NameDbs.FirstOrDefault() ? "default(en)" : System.IO.Path.GetFileNameWithoutExtension(p),
        }).ToList();
        // 恢复勾选
        NameDbsList.SelectionChanged -= OnNameDbsSelectionChanged;
        NameDbsList.SelectedItems.Clear();
        var selected = _viewModel.Session?.SelectedNameDbs ?? [];
        foreach (var item in NameDbsList.Items.Cast<NameDbItem>())
        {
            if (selected.Contains(item.Path))
            {
                NameDbsList.SelectedItems.Add(item);
            }
        }

        NameDbsList.SelectionChanged += OnNameDbsSelectionChanged;
    }

    private void OnSystemSearchChanged(object sender, TextChangedEventArgs e)
        => ApplySystemFilter(SystemSearchBox.Text);

    /// <summary>星系列表默认列出全部（含特殊星系，与 WinUI ShowSpecial=True 一致），搜索就地过滤。</summary>
    private void ApplySystemFilter(string? keyword)
    {
        var source = _viewModel.MapSolarSystems;
        var text = keyword?.Trim();
        SystemList.ItemsSource = string.IsNullOrEmpty(text)
            ? source
            : source.Where(p => p.SolarSystemName?.Contains(text, StringComparison.OrdinalIgnoreCase) == true
                || p.SolarSystemID.ToString().Contains(text)).ToList();
    }

    private void OnSystemSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SystemList.SelectedItem is MapSolarSystem system && _viewModel.Session is not null)
        {
            _viewModel.Session.SetSearchSolarSystem(system);
            LocalSystemToggle.IsChecked = false;
        }
    }

    private void OnNameDbsSelectionChanged(object sender, SelectionChangedEventArgs e)
        => _viewModel.SetSelectedNameDbs(NameDbsList.SelectedItems.Cast<NameDbItem>().Select(p => p.Path));

    // ---------- 按钮 ----------

    private async void OnStartClick(object sender, RoutedEventArgs e) => await _viewModel.StartAsync();

    private void OnStopClick(object sender, RoutedEventArgs e) => _viewModel.StopSelected();

    private void OnStopSoundClick(object sender, RoutedEventArgs e) => _viewModel.StopSound();

    private void OnRestorePosClick(object sender, RoutedEventArgs e) => _viewModel.RestorePos();

    private async void OnStartAllClick(object sender, RoutedEventArgs e) => await _viewModel.StartAllAsync();

    private void OnStopAllClick(object sender, RoutedEventArgs e) => _viewModel.StopAll();

    private async void OnRefreshChannelsClick(object sender, RoutedEventArgs e) => await _viewModel.RefreshChannelsAsync();

    private async void OnRefreshCharactersClick(object sender, RoutedEventArgs e) => await _viewModel.RefreshCharactersAsync();

    private void OnApplySettingToAllClick(object sender, RoutedEventArgs e) => _viewModel.ApplySettingToAll();

    private void OnPickSoundFileClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ChannelIntelSoundSetting setting)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Audio|*.mp3;*.wav;*.wma;*.flac|All|*.*",
            };
            if (dialog.ShowDialog() == true)
            {
                setting.FilePath = dialog.FileName;
            }
        }
    }

    // ---------- 频道内容显示 ----------

    private void ChatContents_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Add)
        {
            return;
        }

        foreach (IntelChatContent content in e.NewItems!)
        {
            AppendChatContent(content);
        }

        TrimChatContents();
        ChatContentsBox.ScrollToEnd();
    }

    private void ZKBIntelContents_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Add)
        {
            return;
        }

        foreach (TheGuideToTheNewEden.Core.Models.EarlyWarningContent content in e.NewItems!)
        {
            AppendLine(GetResourceBrush("SystemFillColorCriticalBrush", Brushes.OrangeRed), $"[{content.Time:HH:mm:ss}] ZKB > {content.Content}", null);
        }

        TrimChatContents();
        ChatContentsBox.ScrollToEnd();
    }

    private void AppendChatContent(IntelChatContent content)
    {
        // 格式：<角色> [EVE时间] 发言人 > 内容（预警=危险色+舰船名加粗；解除=成功色）
        var header = $"{content.Listener} [{content.EVETime:HH:mm:ss}] {content.SpeakerName} > ";
        Brush intelBrush = GetResourceBrush("SystemFillColorCriticalBrush", Brushes.OrangeRed);
        Brush clearBrush = GetResourceBrush("SystemFillColorSuccessBrush", Brushes.SeaGreen);
        var bodyBrush = content.IntelType switch
        {
            IntelChatType.Intel => intelBrush,
            IntelChatType.Clear => clearBrush,
            _ => (Brush?)null,
        };

        if (bodyBrush is null)
        {
            AppendLine(null, header + content.Content, null);
        }
        else
        {
            AppendLine(bodyBrush, header + content.Content, content.IntelShips);
        }
    }

    private void AppendLine(Brush? bodyBrush, string text, List<IntelShipContent>? intelShips)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
        // 头部用次要色，正文按预警类型着色
        var headerLength = text.IndexOf("> ", StringComparison.Ordinal);
        if (headerLength > 0 && bodyBrush is not null)
        {
            paragraph.Inlines.Add(new Run(text[..(headerLength + 2)])
            {
                Foreground = GetResourceBrush("TextFillColorSecondaryBrush", Brushes.Gray),
            });
            paragraph.Inlines.Add(new Run(text[(headerLength + 2)..]) { Foreground = bodyBrush });
        }
        else
        {
            paragraph.Inlines.Add(new Run(text)
            {
                Foreground = bodyBrush ?? GetResourceBrush("TextFillColorPrimaryBrush", Brushes.Black),
            });
        }

        // 舰船名加粗（预警内容里的片段）
        if (bodyBrush is not null && intelShips is { Count: > 0 } && headerLength > 0)
        {
            // 重建正文：按舰船片段拆分加粗
            var body = text[(headerLength + 2)..];
            paragraph.Inlines.Clear();
            paragraph.Inlines.Add(new Run(text[..(headerLength + 2)])
            {
                Foreground = GetResourceBrush("TextFillColorSecondaryBrush", Brushes.Gray),
            });
            var startIndex = 0;
            foreach (var ship in intelShips)
            {
                if (ship.StartIndex > startIndex)
                {
                    paragraph.Inlines.Add(new Run(body[startIndex..ship.StartIndex]) { Foreground = bodyBrush });
                }

                if (ship.StartIndex + ship.Length <= body.Length)
                {
                    paragraph.Inlines.Add(new Run(body.Substring(ship.StartIndex, ship.Length))
                    {
                        Foreground = bodyBrush,
                        FontWeight = FontWeights.Black,
                    });
                }

                startIndex = ship.StartIndex + ship.Length;
            }

            if (startIndex < body.Length)
            {
                paragraph.Inlines.Add(new Run(body[startIndex..]) { Foreground = bodyBrush });
            }
        }

        ChatContentsBox.Document.Blocks.Add(paragraph);
    }

    /// <summary>超出设置的最大显示条数时裁掉最早的段落。</summary>
    private void TrimChatContents()
    {
        var max = Math.Max(1, GameLogsSettingService.MaxShowItems);
        while (ChatContentsBox.Document.Blocks.Count > max && ChatContentsBox.Document.Blocks.FirstBlock is { } first)
        {
            ChatContentsBox.Document.Blocks.Remove(first);
        }
    }

    private void OnClearContentsClick(object sender, RoutedEventArgs e)
    {
        ChatContentsBox.Document.Blocks.Clear();
        _viewModel.ChatContents.Clear();
    }

    private static Brush GetResourceBrush(string key, Brush fallback)
        => Application.Current?.TryFindResource(key) as Brush ?? fallback;
}
