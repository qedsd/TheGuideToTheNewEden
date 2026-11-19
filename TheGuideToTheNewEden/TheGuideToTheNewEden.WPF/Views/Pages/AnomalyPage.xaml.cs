using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.ViewModels;
using TheGuideToTheNewEden.WPF.Views.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 「攻略 · 战斗异常」页：第一个 Tab 固定是异常对照表（列表 + 搜索 + 筛选）。
/// <para>
/// 交互（2026-11-18 按用户要求调整，第三次）：
/// ① **单击中文名** → 在本页新开页签查看该异常正文；
/// ② **双击**整行 → 同上（单击行本身只选中，不开页签）；
/// ③ 点「可升级为」列 → **只有这一列**跳转「死亡远征」页查看它升级成的站点；
/// ④ 页签头可手动关闭。
/// </para>
/// </summary>
public partial class AnomalyPage : Page
{
    private readonly AnomalyPageViewModel _viewModel = new();

    /// <summary>已打开的详情页签：站点 Id → TabItem（重复选中只切换，不重复开）。</summary>
    private readonly Dictionary<int, TabItem> _openTabs = new();

    private bool _loaded;

    public AnomalyPage()
    {
        InitializeComponent();

        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 页面是常驻缓存的（NavigationCacheMode=Required），数据只查第一次。
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        await _viewModel.InitAsync();
    }

    /// <summary>单击中文名 → 在本页新开页签查看该异常正文。</summary>
    private void OnSelfClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AnomalyRow row })
        {
            OpenSiteTab(row.Id);
        }
    }

    /// <summary>双击整行 → 在本页新开页签看该站点正文。</summary>
    private void OnRowDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is DataGrid { SelectedItem: AnomalyRow row })
        {
            OpenSiteTab(row.Id);
        }
    }

    /// <summary>点「可升级为」列里的可点文字 → 跳「死亡远征」页看该站点的详细攻略。</summary>
    private void OnTargetClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AnomalyRow row })
        {
            AnomalyPageViewModel.OpenTarget(row);
        }
    }

    private async void OpenSiteTab(int siteId)
    {
        if (siteId <= 0)
        {
            return;
        }

        // 已经开着就直接切过去
        if (_openTabs.TryGetValue(siteId, out var existing))
        {
            SelectTab(existing);
            return;
        }

        CombatSite? site;
        try
        {
            site = await CombatSiteService.GetByIdAsync(siteId);
        }
        catch (System.Exception ex)
        {
            Core.Log.Error(ex);
            return;
        }

        if (site is null)
        {
            return;
        }

        var tab = new TabItem
        {
            Header = BuildHeader(site),
            Content = new CombatSiteDetailControl { DataContext = site },
        };

        _openTabs[siteId] = tab;
        DetailTabs.Items.Add(tab);
        SelectTab(tab);
    }

    /// <summary>
    /// 切到指定页签。
    /// <para>
    /// 双击打开时**必须延后一拍**：双击的第二次 mouse-up 还没处理完，
    /// 此刻直接设 <c>SelectedItem</c> 会被随后的输入处理顶回去，
    /// 表现为"页签建出来了但没切过去"（单击名字没这问题，因为那条路径的事件已经走完）。
    /// </para>
    /// </summary>
    private void SelectTab(TabItem tab)
    {
        DetailTabs.SelectedItem = tab;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (DetailTabs.Items.Contains(tab))
            {
                DetailTabs.SelectedItem = tab;
                tab.Focus();
            }
        }), DispatcherPriority.Input);
    }

    /// <summary>页签头：站点名 + 关闭按钮（关闭按页签自身从 TabControl 移除）。</summary>
    private UIElement BuildHeader(CombatSite site)
    {
        var title = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(site.TitleCN) ? site.TitleEN : site.TitleCN,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 220,
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");

        var close = new Button
        {
            Content = "\uE711",                 // Fluent 的 Cancel 字形
            FontFamily = new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
            FontSize = 10,
            Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(4, 2, 4, 2),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = site.TitleEN,
        };
        close.SetResourceReference(ForegroundProperty, "TextFillColorSecondaryBrush");
        close.Click += (_, _) => CloseTab(site.Id);

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(title);
        panel.Children.Add(close);
        return panel;
    }

    private void CloseTab(int siteId)
    {
        if (!_openTabs.TryGetValue(siteId, out var tab))
        {
            return;
        }

        _openTabs.Remove(siteId);
        DetailTabs.Items.Remove(tab);
        DetailTabs.SelectedIndex = 0;
    }
}
