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
/// 主页视图模型：快捷方式磁贴（含编辑态）+ 品牌/状态条。
/// <para>
/// 数据全部来自 <see cref="HomeShortcutService"/>（单一数据源），因此「导航右键固定」「添加功能窗口」
/// 「主页编辑」三处入口天然同步——服务发出 <c>Changed</c> 后这里重建磁贴。
/// </para>
/// <para>
/// 磁贴的点击 / 拖动 / 右键在页面代码后置里处理（与本项目其余页面一致，不使用 Command）。
/// </para>
/// </summary>
public sealed class HomePageViewModel : INotifyPropertyChanged
{
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _statusTimer;
    private bool _isEditing;
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

    /// <summary>磁贴（末尾固定是「＋ 添加功能」槽位）。</summary>
    public ObservableCollection<HomeShortcutTileViewModel> Tiles { get; } = [];

    /// <summary>分组显示（P3 分段）时的一段段磁贴。</summary>
    public ObservableCollection<HomeShortcutGroupViewModel> Groups { get; } = [];

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

    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (!SetProperty(ref _isEditing, value))
            {
                return;
            }

            foreach (var tile in Tiles)
            {
                tile.IsEditing = value;
                tile.IsDropTarget = false;
                tile.IsDragging = false;
            }

            OnPropertyChanged(nameof(EditButtonText));
        }
    }

    /// <summary>「编辑 / 完成」按钮文案。</summary>
    public string EditButtonText => Resolve(IsEditing ? "Home.Quick.Done" : "Home.Quick.Edit");

    /// <summary>「共 N 个 · 上限 M」。</summary>
    public string CountText => string.Format(
        Resolve("Home.Quick.Count"), HomeShortcutService.Items.Count, HomeShortcutService.MaxItems);

    /// <summary>是否还能继续固定（达到上限后「＋ 添加」禁用）。</summary>
    public bool CanAdd => !HomeShortcutService.IsFull;

    public bool IsEmpty => HomeShortcutService.Items.Count == 0;

    // ---------- 显示策略（P1 / P2 / P3） ----------

    public bool ShowWalletCard => HomeShortcutService.ShowWalletCard;

    public bool ShowMarketCard => HomeShortcutService.ShowMarketCard;

    public bool ShowRecent => HomeShortcutService.ShowRecent && RecentTiles.Count > 0;

    public bool ShowGrouped => HomeShortcutService.ShowGrouped;

    /// <summary>平铺显示（非分组模式）。</summary>
    public bool ShowFlat => !HomeShortcutService.ShowGrouped;

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

        foreach (var group in Groups)
        {
            group.Refresh();
        }

        foreach (var tile in RecentTiles)
        {
            tile.RefreshTexts();
        }

        Wallet.RefreshTexts();
        Market.RefreshTexts();

        OnPropertyChanged(nameof(AppName));
        OnPropertyChanged(nameof(NewVersionText));
        OnPropertyChanged(nameof(EditButtonText));
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

    /// <summary>点击磁贴：编辑态不跳转；「＋」打开添加功能窗口。</summary>
    public void OpenTile(HomeShortcutTileViewModel tile)
    {
        if (tile.IsAddSlot)
        {
            Views.Windows.AddShortcutWindow.ShowWindow();
            return;
        }

        if (IsEditing)
        {
            return;
        }

        HomeFeatureLauncher.Open(tile.Feature, tile.Param);

        // 「最近使用」只在开关打开时记录（关闭时 PushRecent 内部直接 return）。
        if (HomeShortcutService.ShowRecent)
        {
            HomeShortcutService.PushRecent(tile.Id);
        }
    }

    public void RemoveTile(HomeShortcutTileViewModel tile)
    {
        if (!tile.IsAddSlot)
        {
            // 只删这一块：带参数的磁贴（如"市场 + 某物品"）不会连带删掉同功能的其它磁贴。
            HomeShortcutService.RemoveExact(tile.Id, tile.Param);
        }
    }

    public void MoveTileToFront(HomeShortcutTileViewModel tile)
    {
        if (!tile.IsAddSlot)
        {
            HomeShortcutService.MoveTo(tile.Id, tile.Param, 0);
        }
    }

    public void ToggleTileSize(HomeShortcutTileViewModel tile)
    {
        if (!tile.IsAddSlot)
        {
            HomeShortcutService.SetSize(tile.Id, tile.Param, !tile.IsWide);
        }
    }

    /// <summary>键盘移动（Alt + ←/→）。</summary>
    public void MoveTileByOffset(HomeShortcutTileViewModel tile, int offset)
    {
        if (tile.IsAddSlot || tile.Item is null)
        {
            return;
        }

        var index = IndexOf(tile.Item.Key);
        if (index < 0)
        {
            return;
        }

        HomeShortcutService.MoveTo(tile.Id, tile.Param, index + offset);
    }

    /// <summary>
    /// 拖动落位。<paramref name="targetIndex"/> 是"落在哪个磁贴的位置上"，
    /// <paramref name="insertBefore"/> 决定落在它的前半还是后半；这里换算成移动完成后的最终下标
    /// （同一次移动里，源在目标之前时移除会让目标下标前移，必须补偿）。
    /// </summary>
    public void DropTile(string draggedKey, int targetIndex, bool insertBefore)
    {
        var items = HomeShortcutService.Items.ToList();
        var current = items.FindIndex(p => p.Key == draggedKey);
        if (current < 0 || items.Count == 0)
        {
            return;
        }

        var target = insertBefore ? targetIndex : targetIndex + 1;
        if (current < target)
        {
            target--;
        }

        var item = items[current];
        HomeShortcutService.MoveTo(item.Id, item.Param, Math.Clamp(target, 0, items.Count - 1));
    }

    public void DropTileAtEnd(string draggedKey)
    {
        var items = HomeShortcutService.Items.ToList();
        if (items.FirstOrDefault(p => p.Key == draggedKey) is { } item)
        {
            HomeShortcutService.MoveTo(item.Id, item.Param, items.Count - 1);
        }
    }

    public void RestoreDefaults()
    {
        HomeShortcutService.ResetToDefault();
        IsEditing = false;
    }

    public void ToggleEdit() => IsEditing = !IsEditing;

    /// <summary>退出编辑（离开页面 / Esc）。</summary>
    public void EndEdit()
    {
        if (IsEditing)
        {
            IsEditing = false;
        }
    }

    /// <summary>磁贴在列表里的下标（按"功能 + 参数"的唯一键）。</summary>
    public int IndexOf(string key)
    {
        for (var i = 0; i < Tiles.Count; i++)
        {
            if (Tiles[i].Item?.Key == key)
            {
                return i;
            }
        }

        return -1;
    }

    // ---------- 内部 ----------

    private void OnShortcutsChanged() => RefreshTiles();

    /// <summary>按服务的当前数据重建磁贴（结构变化才重建，避免打断拖动中的视觉）。</summary>
    private void RefreshTiles()
    {
        var items = HomeShortcutService.Items;

        // 顺序 / 组成完全一致时只刷新尺寸，不重建容器。
        var sameShape = Tiles.Count == items.Count + 1;
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
                    Tiles.Add(new HomeShortcutTileViewModel(item, feature) { IsEditing = IsEditing });
                }
            }

            Tiles.Add(new HomeShortcutTileViewModel { IsEditing = IsEditing });
        }

        RebuildGroups();
        RebuildRecent();
        RefreshBadges();

        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(CanAdd));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ShowRecent));
        OnPropertyChanged(nameof(ShowGrouped));
        OnPropertyChanged(nameof(ShowFlat));
    }

    /// <summary>分组显示用：按功能目录里的分组键把磁贴切成一段段（保持用户在主页里排的顺序）。</summary>
    private void RebuildGroups()
    {
        Groups.Clear();

        var tiles = Tiles.Where(p => !p.IsAddSlot && p.Feature is not null).ToList();
        if (tiles.Count == 0)
        {
            return;
        }

        // 分组顺序 = 功能目录里分组首次出现的顺序（与"添加功能"窗口左侧一致），
        // 组内顺序 = 主页上的磁贴顺序。
        foreach (var groupKey in HomeFeatureCatalog.GroupKeys)
        {
            var groupTiles = tiles.Where(p => p.Feature!.GroupKey == groupKey).ToList();
            if (groupTiles.Count > 0)
            {
                Groups.Add(new HomeShortcutGroupViewModel(groupKey, groupTiles) { });
            }
        }
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
