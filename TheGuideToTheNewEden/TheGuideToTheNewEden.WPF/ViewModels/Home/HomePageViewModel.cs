using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services.Home;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.ViewModels.Home;

/// <summary>
/// 主页视图模型：快捷方式磁贴 + 品牌/状态条。
/// <para>
/// 数据全部来自 <see cref="HomeShortcutService"/>（单一数据源），任何入口（导航右键固定 /
/// 设置 → 主页编辑）改动后这里通过 <c>Changed</c> 事件重建磁贴。阶段 101 起主页磁贴只有
/// "点开"一种交互——编辑（增删 / 排序 / 宽窄 / 恢复默认）全部在 设置 → 主页。
/// </para>
/// </summary>
public sealed class HomePageViewModel : INotifyPropertyChanged
{
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _statusTimer;
    private bool _initialized;
    private string _playersText = string.Empty;
    private string _gameTimeText = string.Empty;
    private string? _latestVersion;

    public HomePageViewModel()
    {
        _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _clockTimer.Tick += (_, _) => GameTimeText = DateTime.UtcNow.ToString("HH:mm:ss");

        // 角标用的运行态里，有三处（预警 / 监控 / 多开）没有可订阅的事件，
        // 所以只在主页**可见期间**用一个 3 秒定时器刷新（离开页面即停，不是后台轮询）。
        _statusTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(3),
        };
        _statusTimer.Tick += (_, _) => RefreshBadges();

        HomeShortcutService.Changed += OnShortcutsChanged;
    }

    /// <summary>钱包卡（P1）。</summary>
    public HomeWalletCardViewModel Wallet { get; } = new();

    /// <summary>行情卡（P1）。</summary>
    public HomeMarketCardViewModel Market { get; } = new();

    /// <summary>磁贴（平铺一份；编辑在 设置 → 主页，不再有添加槽位）。</summary>
    public ObservableCollection<HomeShortcutTileViewModel> Tiles { get; } = [];

    /// <summary>「最近使用」条（P2；开关打开时才显示）。</summary>
    public ObservableCollection<HomeShortcutTileViewModel> RecentTiles { get; } = [];

    // ---------- 品牌 / 状态条 ----------

    public string AppName => Resolve("AppDisplayName");

    public string VersionText => AppVersion.Current;

    /// <summary>在线人数（取不到时显示 "—"）。</summary>
    public string PlayersText
    {
        get => _playersText;
        private set => SetProperty(ref _playersText, value);
    }

    /// <summary>世界时（UTC，与 WinUI 版的 EVE 时间一致）。</summary>
    public string GameTimeText
    {
        get => _gameTimeText;
        private set => SetProperty(ref _gameTimeText, value);
    }

    public bool HasNewVersion => _latestVersion is not null;

    public string NewVersionText =>
        _latestVersion is null ? string.Empty : string.Format(Resolve("Home.Status.NewVersion"), _latestVersion);

    // ---------- 快捷方式卡 ----------

    /// <summary>「共 N 个 · 上限 M」。</summary>
    public string CountText => string.Format(
        Resolve("Home.Quick.Count"), HomeShortcutService.Items.Count, HomeShortcutService.MaxItems);

    public bool IsEmpty => HomeShortcutService.Items.Count == 0;

    // ---------- 显示策略（P1 / P2） ----------

    public bool ShowWalletCard => HomeShortcutService.ShowWalletCard;

    public bool ShowMarketCard => HomeShortcutService.ShowMarketCard;

    public bool ShowRecent => HomeShortcutService.ShowRecent && RecentTiles.Count > 0;

    // ---------- 生命周期 ----------

    /// <summary>进入主页时调用（首次会拉一次服务器状态 / 更新检查 / 两张卡片）。</summary>
    public async Task InitAsync()
    {
        RefreshTiles();

        if (!_initialized)
        {
            _initialized = true;
            GameTimeText = DateTime.UtcNow.ToString("HH:mm:ss");
            _ = RefreshServerStatusAsync();
            _ = CheckUpdateAsync();
        }

        if (HomeShortcutService.ShowWalletCard)
        {
            await Wallet.LoadAsync();
        }

        if (HomeShortcutService.ShowMarketCard)
        {
            await Market.LoadAsync();
        }
    }

    public void StartClock()
    {
        _clockTimer.Start();
        _statusTimer.Start();
        RefreshBadges();
    }

    public void StopClock()
    {
        _clockTimer.Stop();
        _statusTimer.Stop();
    }

    public void Dispose()
    {
        _clockTimer.Stop();
        _statusTimer.Stop();
        HomeShortcutService.Changed -= OnShortcutsChanged;
    }

    /// <summary>语言切换后刷新所有由代码拼出来的文案（磁贴标题走资源键，需逐个通知）。</summary>
    public void RefreshTexts()
    {
        foreach (var tile in Tiles)
        {
            tile.RefreshTexts();
        }

        foreach (var tile in RecentTiles)
        {
            tile.RefreshTexts();
        }

        Wallet.RefreshTexts();
        Market.RefreshTexts();

        OnPropertyChanged(nameof(AppName));
        OnPropertyChanged(nameof(NewVersionText));
        OnPropertyChanged(nameof(CountText));
    }

    /// <summary>刷新磁贴角标（运行态）。</summary>
    public void RefreshBadges()
    {
        foreach (var tile in Tiles)
        {
            tile.RefreshBadge();
        }

        foreach (var tile in RecentTiles)
        {
            tile.RefreshBadge();
        }
    }

    // ---------- 磁贴操作 ----------

    /// <summary>点击磁贴：打开对应功能并记录"最近使用"。</summary>
    public void OpenTile(HomeShortcutTileViewModel tile)
    {
        HomeFeatureLauncher.Open(tile.Feature, tile.Param);

        // 「最近使用」只在开关打开时记录（关闭时 PushRecent 内部直接 return）。
        if (HomeShortcutService.ShowRecent)
        {
            HomeShortcutService.PushRecent(tile.Id);
        }
    }

    // ---------- 内部 ----------

    private void OnShortcutsChanged()
    {
        RefreshTiles();

        // 「⋯」开关把卡片打开后立刻补一次加载——两个 VM 自带"已加载"守卫（_loaded），
        // 重复调用是空操作；没有这一步，卡片虽出现了，却显示"还没有登录角色 / 还没有收藏物品"
        // 的空引导态，要等下次进主页才取数。
        if (HomeShortcutService.ShowWalletCard)
        {
            _ = Wallet.LoadAsync();
        }

        if (HomeShortcutService.ShowMarketCard)
        {
            _ = Market.LoadAsync();
        }
    }

    /// <summary>按服务的当前数据重建磁贴（结构变化才重建，避免打断拖动中的视觉）。</summary>
    private void RefreshTiles()
    {
        var items = HomeShortcutService.Items;

        // 顺序 / 组成完全一致时只刷新尺寸，不重建容器。
        var sameShape = Tiles.Count == items.Count;
        if (sameShape)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (Tiles[i].Item?.Key != items[i].Key)
                {
                    sameShape = false;
                    break;
                }
            }
        }

        if (sameShape)
        {
            for (var i = 0; i < items.Count; i++)
            {
                Tiles[i].SetWide(items[i].IsWide);
            }
        }
        else
        {
            Tiles.Clear();
            foreach (var item in items)
            {
                if (HomeFeatureCatalog.Find(item.Id) is { } feature)
                {
                    Tiles.Add(new HomeShortcutTileViewModel(item, feature));
                }
            }
        }

        RebuildRecent();
        RefreshBadges();

        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsEmpty));
        // 两张数据卡的可见性也在这份"服务变化 → 通知"清单里：
        // 它们是 `=> HomeShortcutService.Show*` 的计算属性，缺了这两行就只在绑定时读一次初值，
        // 设置页里的显示开关拨了也不动（阶段 92 追加一引入的漏通知，阶段 95 修复）。
        OnPropertyChanged(nameof(ShowWalletCard));
        OnPropertyChanged(nameof(ShowMarketCard));
        OnPropertyChanged(nameof(ShowRecent));
    }

    /// <summary>「最近使用」条：按记录顺序（最新在前）取功能磁贴。</summary>
    private void RebuildRecent()
    {
        RecentTiles.Clear();

        if (!HomeShortcutService.ShowRecent)
        {
            return;
        }

        foreach (var id in HomeShortcutService.Recent)
        {
            if (HomeFeatureCatalog.Find(id) is { } feature)
            {
                // 复用同一个磁贴 VM；这里不参与拖动/编辑，只用于点开。
                RecentTiles.Add(new HomeShortcutTileViewModel(
                    new HomeShortcutItem { Id = feature.Id }, feature));
            }
        }
    }

    /// <summary>在线人数（ESI 匿名接口；失败不影响主页其余部分）。</summary>
    private async Task RefreshServerStatusAsync()
    {
        PlayersText = Resolve("Home.Status.PlayersUnknown");
        try
        {
            var response = await Core.Services.ESIService.GetDefaultESI().Status.GetStatusAsync();
            var players = response?.Model?.Players ?? 0;
            PlayersText = players > 0
                ? string.Format(Resolve("Home.Status.Players"), players.ToString("N0"))
                : Resolve("Home.Status.PlayersUnknown");
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"[主页] 取服务器状态失败：{ex.Message}");
            PlayersText = Resolve("Home.Status.PlayersUnknown");
        }
    }

    /// <summary>启动时静默检查新版本（与"设置 → 软件更新"同一数据源，弹窗留给该页面）。</summary>
    private async Task CheckUpdateAsync()
    {
        if (!AutoUpdateService.Value)
        {
            return;
        }

        try
        {
            var release = await Core.Helpers.GithubHelper.GetLastReleaseInfoAsync();
            var latest = release.TagName.TrimStart('v', 'V');
            var current = AppVersion.Current.Split('-', '+')[0];

            if (Version.TryParse(latest, out var latestVersion)
                && Version.TryParse(current, out var currentVersion)
                && latestVersion > currentVersion)
            {
                _latestVersion = latest;
                OnPropertyChanged(nameof(HasNewVersion));
                OnPropertyChanged(nameof(NewVersionText));
            }
        }
        catch (Exception ex)
        {
            // 网络不可用 / GitHub 限流等属**常见外部状况**（匿名接口每小时 60 次），只记 Info，
            // 不写 WARN：否则每次启动都在日志里刷一条看起来像故障的记录。
            Core.Log.Info($"[主页] 检查更新未完成：{ex.Message}");
        }
    }

    private static string Resolve(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
