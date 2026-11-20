using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TheGuideToTheNewEden.Core.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.CombatLog;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 日志监控小窗（阶段 359 重设计）：
/// <list type="bullet">
///   <item>上半部战斗信息：二值状态（反跳 / 干扰）用胶囊，数值（毁电 / 遥修 / 吸电 / 传电 / 承伤）用卡片；</item>
///   <item>下半部关键词命中：单行消息流，最新一条高亮；</item>
///   <item>只开战斗时卡片区撑满，只开监控时消息区撑满，两个都开时上下分区；</item>
///   <item>标题可带当前舰船类型（ESI 读取，开关在日志监控的"战斗日志"设置里）。</item>
/// </list>
/// </summary>
public partial class GameLogMsgWindow : Wpf.Ui.Controls.FluentWindow
{
    private bool _combatOnly;
    private bool _showShip;
    private string _baseTitle;
    private string? _shipName;
    private Func<string>? _titleProvider;
    private readonly ObservableCollection<CombatPill> _pills = new();
    private readonly ObservableCollection<CombatCard> _cards = new();
    private readonly ObservableCollection<FeedRow> _feed = new();
    private bool _closing;
    private int _feedCount;

    /// <summary>窗口被隐藏（用户关闭/手动隐藏）：通知服务停声音、清系统通知。</summary>
    public event EventHandler? OnHided;

    /// <summary>点击"前置游戏"按钮。</summary>
    public event EventHandler? OnShowGameButtonClick;

    public string ListenerName { get; }

    private bool _allowClose;

    public GameLogMsgWindow(string listenerName, string? displayTitle = null, bool combatOnly = false, bool showShip = false, Func<string>? titleProvider = null)
    {
        ListenerName = listenerName;
        _combatOnly = combatOnly;
        _showShip = showShip;
        _baseTitle = displayTitle ?? listenerName;
        _titleProvider = titleProvider;

        InitializeComponent();
        TitleBar.Title = _baseTitle;
        PillsList.ItemsSource = _pills;
        CardsList.ItemsSource = _cards;
        FeedList.ItemsSource = _feed;

        CombatLogStateCenter.Current.Changed += OnCombatChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;
        RefreshCombat();
        UpdateFeedState();

        if (_showShip)
        {
            _ = LoadShipAsync();
        }
    }

    /// <summary>把"仅战斗"窗口升级成"战斗 + 监控"双区（关键词日志复用同一窗口时调用）。</summary>
    public void EnableMonitor()
    {
        if (!_combatOnly)
        {
            return;
        }

        _combatOnly = false;
        RefreshCombat();
        UpdateFeedState();
    }

    /// <summary>关键词日志停止、但战斗日志还在跑时，收掉监控区，恢复成仅战斗窗口。</summary>
    public void DisableMonitor()
    {
        if (_combatOnly)
        {
            return;
        }

        _combatOnly = true;
        RefreshCombat();
        UpdateFeedState();
    }

    /// <summary>复用窗口时更新标题（关键词配置名可能和战斗日志窗口不同）。</summary>
    public void SetTitle(string title)
    {
        _titleProvider = null;
        _baseTitle = title;
        UpdateTitle();
    }

    /// <summary>复用窗口时传入标题生成器，切换语言后标题跟着变。</summary>
    public void SetTitleProvider(Func<string> titleProvider)
    {
        _titleProvider = titleProvider;
        UpdateTitle();
    }

    /// <summary>切换标题里的舰船显示（不重建窗口）。</summary>
    public void SetShowShip(bool show)
    {
        if (_showShip == show)
        {
            return;
        }

        _showShip = show;
        if (show)
        {
            _ = LoadShipAsync();
        }
        else
        {
            _shipName = null;
            UpdateTitle();
        }
    }

    private void UpdateTitle()
    {
        var title = _titleProvider?.Invoke();
        if (!string.IsNullOrWhiteSpace(title))
        {
            _baseTitle = title;
        }

        TitleBar.Title = string.IsNullOrWhiteSpace(_shipName) ? _baseTitle : $"{_baseTitle} · {_shipName}";
    }

    private async Task LoadShipAsync()
    {
        try
        {
            var ship = await CharacterShipNameService.GetAsync(ListenerName);
            if (!_closing && !string.IsNullOrWhiteSpace(ship))
            {
                _shipName = ship;
                UpdateTitle();
            }
        }
        catch
        {
            // 取不到舰船就只显示角色名
        }
    }

    /// <summary>关闭即隐藏（窗口要复用，直接关掉会导致下次 Show() 抛异常）。</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideWindow();
        }

        base.OnClosing(e);
    }

    /// <summary>真正销毁窗口（通知服务 Remove 时调用）。</summary>
    public void CloseWindow()
    {
        _allowClose = true;
        CombatLogStateCenter.Current.Changed -= OnCombatChanged;
        LanguageService.LanguageChanged -= OnLanguageChanged;
        _closing = true;
        Close();
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    /// <summary>显示窗口并追加一条消息。</summary>
    public void Show(string content, string? channel = null)
    {
        AppendMessage(content, channel);
        if (!IsVisible)
        {
            Show();
        }
    }

    /// <summary>只显示窗口，不追加消息（战斗日志小窗用）。</summary>
    public void ShowWindow()
    {
        if (_combatOnly && CombatLogStateCenter.Current.GetRows(ListenerName).Count == 0)
        {
            return;
        }

        if (!IsVisible)
        {
            Show();
        }
    }

    public void AppendMessage(string content, string? channel = null)
    {
        if (_combatOnly)
        {
            return;
        }

        foreach (var row in _feed)
        {
            row.IsNew = false;
        }

        _feed.Insert(0, new FeedRow
        {
            Time = DateTime.Now.ToString("HH:mm"),
            Channel = channel ?? string.Empty,
            Text = content,
            IsNew = true,
        });

        while (_feed.Count > 200)
        {
            _feed.RemoveAt(_feed.Count - 1);
        }

        UpdateFeedState();
        FeedScroll.ScrollToTop();
    }

    /// <summary>清空命中列表（战斗状态单独清）。</summary>
    public void Clear()
    {
        _feed.Clear();
        UpdateFeedState();
    }

    /// <summary>隐藏窗口（不关闭，等下一条消息再显示），并通知停声音/清系统通知。</summary>
    public void HideWindow()
    {
        if (IsVisible)
        {
            Hide();
            OnHided?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnClearFeedClick(object sender, RoutedEventArgs e)
    {
        Clear();
    }

    private void OnShowGameClick(object sender, RoutedEventArgs e)
        => OnShowGameButtonClick?.Invoke(this, EventArgs.Empty);

    private void UpdateFeedState()
    {
        _feedCount = _feed.Count;
        FeedEmpty.Visibility = _feedCount == 0 ? Visibility.Visible : Visibility.Collapsed;
        FeedClearButton.IsEnabled = _feedCount > 0;
        FeedClearButton.Visibility = _combatOnly ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---------- 战斗信息 ----------

    private void OnLanguageChanged(object? sender, string language)
    {
        if (_closing)
        {
            return;
        }

        UpdateTitle();
        RefreshCombat();
        UpdateFeedState();
    }

    private void OnCombatChanged(string listener)
    {
        if (string.Equals(listener, ListenerName, StringComparison.OrdinalIgnoreCase))
        {
            RefreshCombat();
        }
    }

    private void RefreshCombat()
    {
        if (_closing)
        {
            return;
        }

        var attached = _combatOnly || CombatLogStateCenter.Current.IsAttached(ListenerName);
        var rows = attached ? CombatLogStateCenter.Current.GetRows(ListenerName) : Array.Empty<CombatLogRow>();

        _pills.Clear();
        _cards.Clear();
        var maxCards = _combatOnly ? 6 : 4;
        foreach (var row in rows)
        {
            var definition = CombatCategories.Find(row.Category);
            if (definition is null)
            {
                continue;
            }

            var brush = BrushOf(ColorForCategory(row.Category));
            if (!definition.HasValue)
            {
                _pills.Add(new CombatPill
                {
                    Label = LanguageService.Get(CategoryLabelKey(row.Category)),
                    Brush = brush,
                    Background = TintOf(brush),
                });
            }
        }

        // 只放得下 4 张卡时，优先保住"承伤"（DPS 比传电更值得看），把末位换成它
        var valueRows = rows
            .Where(p => CombatCategories.Find(p.Category) is { HasValue: true })
            .ToList();
        if (valueRows.Count > maxCards)
        {
            var selected = valueRows.Take(maxCards).ToList();
            var damageRow = valueRows.FirstOrDefault(p => p.Category == CombatCategories.Damage);
            if (damageRow is not null && !selected.Contains(damageRow))
            {
                selected[selected.Count - 1] = damageRow;
            }

            valueRows = selected;
        }

        foreach (var row in valueRows)
        {
            var definition = CombatCategories.Find(row.Category);
            var brush = BrushOf(ColorForCategory(row.Category));
            var isDamage = string.Equals(row.Category, CombatCategories.Damage, StringComparison.OrdinalIgnoreCase);
            var value = isDamage ? row.Rate ?? row.LastValue : row.LastValue;
            var unit = isDamage ? "DPS" : definition.Unit;
            _cards.Add(new CombatCard
            {
                Label = LanguageService.Get(CategoryLabelKey(row.Category)),
                Value = FormatAmount(value),
                Unit = unit,
                Window = BuildSecondary(row, definition, isDamage),
                Brush = brush,
            });
        }

        PillsList.Visibility = _pills.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        CardsList.Visibility = _cards.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        CombatClearButton.Visibility = attached && rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        ApplyLayout(attached, rows.Count > 0);

        if (_combatOnly && rows.Count == 0 && IsVisible && WindowState != WindowState.Minimized)
        {
            HideWindow();
        }
        else if (_combatOnly && rows.Count > 0 && !IsVisible && WindowState != WindowState.Minimized)
        {
            ShowWindow();
        }
    }

    private static string BuildSecondary(CombatLogRow row, CombatCategoryDefinition definition, bool isDamage)
    {
        if (isDamage)
        {
            return row.LastValue is { } last ? $"/ {FormatAmount(last)} HP" : string.Empty;
        }

        return row.Rate is { } rate ? $"/ {FormatAmount(rate)} {definition.Unit}/s" : string.Empty;
    }

    private bool _sizingCards;

    private void OnCardsSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCardSizes();
    }

    /// <summary>
    /// 卡片按可用宽度分两列（最后一张奇数时占满整行）；只开战斗时高度均分把区域撑满。
    /// </summary>
    private void UpdateCardSizes()
    {
        if (_sizingCards || _cards.Count == 0)
        {
            return;
        }

        var availableWidth = CardsList.ActualWidth;
        if (availableWidth <= 0)
        {
            return;
        }

        _sizingCards = true;
        try
        {
            var columnWidth = Math.Max(1, Math.Floor((availableWidth - 16) / 2));
            var rows = (_cards.Count + 1) / 2;
            var height = 58.0;
            if (_combatOnly && CardsList.ActualHeight > 0)
            {
                height = Math.Max(58, (CardsList.ActualHeight - rows * 7) / rows);
            }

            for (var i = 0; i < _cards.Count; i++)
            {
                var oddLast = _cards.Count % 2 == 1 && i == _cards.Count - 1;
                _cards[i].Width = oddLast ? Math.Max(1, availableWidth - 7) : columnWidth;
                _cards[i].Height = height;
            }
        }
        finally
        {
            _sizingCards = false;
        }
    }

    private void ApplyLayout(bool attached, bool hasRows)
    {
        var monitor = !_combatOnly;
        var combat = attached && hasRows;
        CombatArea.Visibility = combat ? Visibility.Visible : Visibility.Collapsed;
        MonitorArea.Visibility = monitor ? Visibility.Visible : Visibility.Collapsed;

        CombatRow.Height = combat
            ? (monitor ? GridLength.Auto : new GridLength(1, GridUnitType.Star))
            : new GridLength(0);
        MonitorRow.Height = monitor ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        // 只开战斗时让卡片撑满剩余高度（2 列 3 行），不要都挤在顶部
        var fillCards = combat && !monitor;
        CardsRow.Height = fillCards ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        Dispatcher.BeginInvoke(UpdateCardSizes, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>手动清除战斗行：反跳 / ECM 这类一次性状态没有"结束"日志，只能由用户收掉。</summary>
    private void OnCombatClearClick(object sender, RoutedEventArgs e)
        => CombatLogStateCenter.Current.Clear(ListenerName);

    // ---------- 最小化 ----------

    protected override void OnStateChanged(EventArgs e)
    {
        // 工具窗口 ShowInTaskbar=False 时最小化会丢到屏幕外且没有恢复入口：
        // 最小化期间临时显示任务栏按钮，恢复后再隐藏（与 IntelWindow 一致）。
        ShowInTaskbar = WindowState == WindowState.Minimized;
        base.OnStateChanged(e);
    }

    private static string CategoryLabelKey(string category) => category switch
    {
        CombatCategories.Scram => "CombatCategory_Scram",
        CombatCategories.Neut => "CombatCategory_Neut",
        CombatCategories.Nos => "CombatCategory_Nos",
        CombatCategories.Ecm => "CombatCategory_Ecm",
        CombatCategories.Rep => "CombatCategory_Rep",
        CombatCategories.CapTransfer => "CombatCategory_CapTransfer",
        CombatCategories.Damage => "CombatCategory_Damage",
        _ => category,
    };

    private static Color ColorForCategory(string category) => category switch
    {
        CombatCategories.Scram => Color.FromRgb(0xE5, 0x48, 0x4D),
        CombatCategories.Neut => Color.FromRgb(0xF5, 0xA5, 0x24),
        CombatCategories.Nos => Color.FromRgb(0x3B, 0x82, 0xF6),
        CombatCategories.Ecm => Color.FromRgb(0xA8, 0x55, 0xF7),
        CombatCategories.Rep => Color.FromRgb(0x30, 0xA4, 0x6C),
        CombatCategories.CapTransfer => Color.FromRgb(0x06, 0xB6, 0xD4),
        CombatCategories.Damage => Color.FromRgb(0xF9, 0x73, 0x16),
        _ => Color.FromRgb(0x6B, 0x72, 0x80),
    };

    private static SolidColorBrush BrushOf(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush TintOf(SolidColorBrush brush)
    {
        var tint = new SolidColorBrush(Color.FromArgb(0x1F, brush.Color.R, brush.Color.G, brush.Color.B));
        tint.Freeze();
        return tint;
    }

    private static string FormatAmount(double? value)
        => value is { } number ? number.ToString("0") : "—";

    private static Brush GetResourceBrush(string key, Brush fallback)
        => Application.Current?.TryFindResource(key) as Brush ?? fallback;
}

public sealed class CombatPill
{
    public string Label { get; set; } = string.Empty;

    public Brush Brush { get; set; } = Brushes.Gray;

    public Brush Background { get; set; } = Brushes.Transparent;
}

public sealed class CombatCard : INotifyPropertyChanged
{
    private double _width;
    private double _height;

    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = "—";

    public string Unit { get; set; } = string.Empty;

    public string Window { get; set; } = string.Empty;

    public Brush Brush { get; set; } = Brushes.Gray;

    public double Width
    {
        get => _width;
        set
        {
            if (Math.Abs(_width - value) < 0.5)
            {
                return;
            }

            _width = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Width)));
        }
    }

    public double Height
    {
        get => _height;
        set
        {
            if (Math.Abs(_height - value) < 0.5)
            {
                return;
            }

            _height = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Height)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class FeedRow : INotifyPropertyChanged
{
    private bool _isNew;

    public string Time { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public bool IsNew
    {
        get => _isNew;
        set
        {
            if (_isNew == value)
            {
                return;
            }

            _isNew = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNew)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
