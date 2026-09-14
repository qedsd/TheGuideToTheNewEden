using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Models.KB;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.KB;
using ZKB.NET;
using ZKB.NET.Models.Statistics;

namespace TheGuideToTheNewEden.WPF.ViewModels.KB;

/// <summary>
/// 实体统计页的 ViewModel：左侧实体信息卡 + 右侧 6 个子页签（KB 列表 / 最贵击杀 / 最高击杀 /
/// 分类统计 / 月份统计 / 超期击杀）的全部数据。
///
/// 相对 WinUI 的改进：
/// <list type="list">
///   <item>子页签内容<b>全部懒加载</b>（WinUI 在实体页构造函数里一次性 new 出 5 个子页面），本页只在该 Tab 首次选中时取数；</item>
///   <item>"最近 7 天最高价值"修正为使用 <c>TopIskKills7d</c>（WinUI 误用了历史数据 <c>TopIskKills</c>）；</item>
///   <item>过滤类型下拉与 <see cref="TypeModifier"/> 用显式数组映射，不再依赖 ComboBox 项顺序做强制转换。</item>
/// </list>
/// </summary>
public sealed class EntityStatistViewModel : INotifyPropertyChanged
{
    /// <summary>列表过滤类型（索引 0 = 所有）。</summary>
    public static readonly TypeModifier?[] Modifiers =
    [
        null,
        TypeModifier.Kills,
        TypeModifier.Losses,
        TypeModifier.W_space,
        TypeModifier.Solo,
        TypeModifier.Finalblow_only,
        TypeModifier.Awox,
        TypeModifier.Npc,
    ];

    private readonly EntityType _entityType;
    private readonly CancellationTokenSource _cts = new();

    private bool _loaded;
    private bool _isLoading;
    private bool _isStatisticLoaded;
    private bool _isKillListLoading;
    private int _busyCount;
    private string _busyText = string.Empty;
    private bool _hasSupers;
    private bool _hasStatistic;
    private int _page = 1;
    private bool _hasNext;
    private int _modifierIndex;
    private string? _avatarUrl;
    private string _title = string.Empty;
    private string _categoryLabel = string.Empty;
    private EntityBaseInfo? _baseInfo;
    private EntityStatistic? _statistic;

    public EntityStatistViewModel(EntityType entityType, int id, string? title)
    {
        _entityType = entityType;
        Id = id;
        _title = string.IsNullOrWhiteSpace(title) ? id.ToString() : title;
        EntityCategory = ZkbMapping.ToCategory(entityType);
        CategoryLabel = FindString(ZkbMapping.ToCategoryLocalizationKey(EntityCategory));
        AvatarUrl = GameImageHelper.BuildEntityImageUrl(EntityCategory, id, 128);

        var modifiers = Modifiers;
        for (var i = 0; i < modifiers.Length; i++)
        {
            ModifierOptions.Add(new ModifierOption(modifiers[i], ModifierLabel(i)));
        }

        Killmails.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsKillListEmpty));
    }

    public int Id { get; }

    public EntityType EntityType => _entityType;

    /// <summary>实体类别（决定头像与跳转）。</summary>
    public IdName.CategoryEnum EntityCategory { get; }

    /// <summary>实体名称（标签标题用）。</summary>
    public string Title
    {
        get => _title;
        private set => Set(ref _title, value);
    }

    /// <summary>类别文本（角色 / 军团 / …）。</summary>
    public string CategoryLabel
    {
        get => _categoryLabel;
        private set => Set(ref _categoryLabel, value);
    }

    /// <summary>头像/徽标地址。</summary>
    public string? AvatarUrl
    {
        get => _avatarUrl;
        private set => Set(ref _avatarUrl, value);
    }

    /// <summary>实体基本信息（归属、成员、安全等级等）。</summary>
    public EntityBaseInfo? BaseInfo
    {
        get => _baseInfo;
        private set => Set(ref _baseInfo, value);
    }

    /// <summary>信息卡的"标签 : 值"行（按类别只显示有值的那几行）。</summary>
    public ObservableCollection<EntityInfoRow> InfoRows { get; } = [];

    /// <summary>危险系数（0~100）。</summary>
    public double DangerRatio => _statistic?.DangerRatio ?? 0;

    public double GangRatio => _statistic?.GangRatio ?? 0;

    public int ItemDestroyed => _statistic?.ItemDestroyed ?? 0;

    public int ItemLost => _statistic?.ItemLost ?? 0;

    public int PointsDestroyed => _statistic?.PointsDestroyed ?? 0;

    public int PointsLost => _statistic?.PointsLost ?? 0;

    public int SoloKills => _statistic?.SoloKills ?? 0;

    public int SoloLosses => _statistic?.SoloLosses ?? 0;

    public string IskDestroyedText => IskFormatHelper.Format(_statistic?.ISKDestroyed ?? 0);

    public string IskLostText => IskFormatHelper.Format(_statistic?.ISKLost ?? 0);

    /// <summary>是否拥有超期（决定第 6 个页签是否显示）。</summary>
    public bool HasSupers
    {
        get => _hasSupers;
        private set => Set(ref _hasSupers, value);
    }

    /// <summary>统计是否已取到。</summary>
    public bool HasStatistic
    {
        get => _hasStatistic;
        private set => Set(ref _hasStatistic, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => Set(ref _isLoading, value);
    }

    public bool IsStatisticLoading => _isLoading && !_isStatisticLoaded;

    /// <summary>
    /// 本页是否有等待中的异步操作（页面局部等待遮罩用）。
    /// 实体页可以同时打开多个并发加载，等待态必须每页独立，不能走全局遮罩；
    /// 可重入计数保证同页内并发触发的多个操作全部结束后才隐藏。
    /// </summary>
    public bool IsBusy => _busyCount > 0;

    /// <summary>局部等待文案。</summary>
    public string BusyText
    {
        get => _busyText;
        private set => Set(ref _busyText, value);
    }

    // ---- KB 列表 ----

    public ObservableCollection<KBItemInfo> Killmails { get; } = [];

    public bool IsKillListEmpty => Killmails.Count == 0 && !_isKillListLoading;

    public int Page
    {
        get => _page;
        private set => Set(ref _page, value);
    }

    public bool HasNext
    {
        get => _hasNext;
        private set => Set(ref _hasNext, value);
    }

    /// <summary>过滤类型下拉的选中索引。</summary>
    public int ModifierIndex
    {
        get => _modifierIndex;
        set
        {
            if (value < 0 || value >= Modifiers.Length)
            {
                value = 0;
            }

            if (Set(ref _modifierIndex, value))
            {
                _ = ReloadKillmailsAsync(forceRefresh: true);
            }
        }
    }

    public List<ModifierOption> ModifierOptions { get; } = [];

    // ---- 其余页签数据 ----

    public ObservableCollection<KillCardItem> TopValueAllTime { get; } = [];

    public ObservableCollection<KillCardItem> TopValue7Days { get; } = [];

    public ObservableCollection<KillStatisticGroup> TopAllTime { get; } = [];

    public ObservableCollection<GroupDataInfo> Groups { get; } = [];

    public ObservableCollection<MonthData> Months { get; } = [];

    public ObservableCollection<KillCardItem> Titans { get; } = [];

    public ObservableCollection<KillCardItem> Supercarriers { get; } = [];

    private bool _topValueLoaded;
    private bool _topAllTimeLoaded;
    private bool _groupsLoaded;

    /// <summary>首次显示（或强制刷新）时加载统计数据与 KB 列表。</summary>
    public async Task LoadAsync(bool forceRefresh = false)
    {
        if (_loaded && !forceRefresh)
        {
            return;
        }

        _loaded = true;
        IsLoading = true;
        OnPropertyChanged(nameof(IsStatisticLoading));

        try
        {
            if (forceRefresh)
            {
                ZkbQueryService.ClearStatisticCache();
            }

            var statistic = await ZkbQueryService.GetStatisticAsync(_entityType, Id, forceRefresh, _cts.Token);
            if (statistic is null)
            {
                PageNotifyService.Error(FindString("ZKBPage_QueryFailed"));
                return;
            }

            _statistic = statistic;
            _isStatisticLoaded = true;
            HasStatistic = true;
            HasSupers = statistic.HasSupers;
            OnStatisticChanged();

            var baseInfo = await ZkbQueryService.BuildEntityBaseInfoAsync(statistic, _cts.Token);
            BaseInfo = baseInfo;
            if (!string.IsNullOrWhiteSpace(baseInfo.Name?.Name))
            {
                Title = baseInfo.Name.Name;
            }

            BuildInfoRows(baseInfo);

            // 月份统计（本地排序，无网络请求）
            Months.Clear();
            foreach (var month in (statistic.Months ?? []).OrderBy(p => p.Year).ThenBy(p => p.Month))
            {
                Months.Add(month);
            }

            await ReloadKillmailsAsync(forceRefresh: true);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsStatisticLoading));
            OnPropertyChanged(nameof(IsKillListEmpty));
        }
    }

    /// <summary>KB 列表翻页 / 切换过滤类型 / 刷新。</summary>
    public async Task ReloadKillmailsAsync(bool forceRefresh = false)
    {
        if (!_isStatisticLoaded)
        {
            return;
        }

        if (forceRefresh)
        {
            Page = 1;
        }

        _isKillListLoading = true;
        OnPropertyChanged(nameof(IsKillListEmpty));

        try
        {
            var page = await ZkbQueryService.GetEntityKillmailsAsync(
                _entityType, Id, Page, Modifiers[_modifierIndex], _cts.Token);

            Killmails.Clear();
            if (page is not null)
            {
                foreach (var item in page.Items)
                {
                    Killmails.Add(item);
                }

                HasNext = page.HasNext;
            }
            else
            {
                HasNext = false;
                PageNotifyService.Error(FindString("ZKBPage_QueryFailed"));
            }
        }
        finally
        {
            _isKillListLoading = false;
            OnPropertyChanged(nameof(IsKillListEmpty));
        }
    }

    /// <summary>翻页（由分页控件调用）。</summary>
    public async Task GoToPageAsync(int page)
    {
        if (page < 1 || page == Page)
        {
            return;
        }

        Page = page;
        await ReloadKillmailsAsync();
    }

    /// <summary>首次切到"最贵击杀"页签时加载。</summary>
    public async Task EnsureTopValueAsync()
    {
        if (_topValueLoaded || _statistic is null)
        {
            return;
        }

        _topValueLoaded = true;

        var allTime = await ZkbQueryService.BuildTopValueAsync(_statistic.TopIskKills ?? [], _cts.Token);
        Fill(TopValueAllTime, allTime);

        // 修正 WinUI 的缺陷：7 天榜应使用 TopIskKills7d
        var sevenDays = await ZkbQueryService.BuildTopValueAsync(_statistic.TopIskKills7d ?? [], _cts.Token);
        Fill(TopValue7Days, sevenDays);
    }

    /// <summary>首次切到"最高击杀"页签时加载。</summary>
    public async Task EnsureTopAllTimeAsync()
    {
        if (_topAllTimeLoaded || _statistic is null)
        {
            return;
        }

        _topAllTimeLoaded = true;

        var groups = await ZkbQueryService.BuildTopAllTimeAsync(_statistic, _cts.Token);
        TopAllTime.Clear();
        foreach (var group in groups)
        {
            TopAllTime.Add(group);
        }
    }

    /// <summary>首次切到"分类统计"页签时加载。</summary>
    public async Task EnsureGroupsAsync()
    {
        if (_groupsLoaded || _statistic is null)
        {
            return;
        }

        _groupsLoaded = true;

        var groups = await ZkbQueryService.BuildGroupStatistAsync(_statistic, _cts.Token);
        Fill(Groups, groups);
    }

    /// <summary>首次切到"超期击杀"页签时加载。</summary>
    public async Task EnsureSupersAsync()
    {
        if (_statistic?.Supers is null)
        {
            return;
        }

        if (Titans.Count == 0 && Supercarriers.Count == 0)
        {
            var titans = await ZkbQueryService.BuildSuperKillsAsync(_statistic.Supers.Titans, _cts.Token);
            Fill(Titans, titans);

            var supercarriers = await ZkbQueryService.BuildSuperKillsAsync(_statistic.Supers.Supercarriers, _cts.Token);
            Fill(Supercarriers, supercarriers);
        }
    }

    public void Cancel() => _cts.Cancel();

    /// <summary>开始一次局部等待（文案以最后一次为准，计数 +1）。</summary>
    public void BeginBusy(string text)
    {
        _busyCount++;
        BusyText = text;
        OnPropertyChanged(nameof(IsBusy));
    }

    /// <summary>结束一次局部等待（计数归零才隐藏遮罩）。</summary>
    public void EndBusy()
    {
        if (_busyCount > 0)
        {
            _busyCount--;
        }

        if (_busyCount == 0)
        {
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    /// <summary>
    /// 按实体类别只装配有值的信息行。行集与 WinUI 实体页一致
    /// （军团/联盟/执行军团/星系/星域/舰船/类别/安全等级/成员；WinUI 未展示 CEO 行，此处同样不展示），
    /// 实体行的值带 <see cref="EntityInfoRow.Link"/>，界面渲染成链接样式、点击跳转该实体。
    /// </summary>
    private void BuildInfoRows(EntityBaseInfo baseInfo)
    {
        InfoRows.Clear();

        void AddLink(string labelKey, IdName? link)
        {
            if (link is not null && !string.IsNullOrWhiteSpace(link.Name))
            {
                InfoRows.Add(new EntityInfoRow(FindString(labelKey), link.Name, link));
            }
        }

        void AddPlain(string labelKey, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                InfoRows.Add(new EntityInfoRow(FindString(labelKey), value));
            }
        }

        switch (baseInfo.Type)
        {
            case IdName.CategoryEnum.Character:
                AddLink("EntityStatistPage_Corporation", baseInfo.CorpName);
                AddLink("EntityStatistPage_Alliance", baseInfo.AllianceName);
                break;
            case IdName.CategoryEnum.Corporation:
                AddLink("EntityStatistPage_Alliance", baseInfo.AllianceName);
                AddPlain("EntityStatistPage_Members", baseInfo.Members?.ToString("N0"));
                break;
            case IdName.CategoryEnum.Alliance:
                AddLink("EntityStatistPage_Executor", baseInfo.ExecutorCorpName);
                AddPlain("EntityStatistPage_Members", baseInfo.Members?.ToString("N0"));
                break;
            case IdName.CategoryEnum.InventoryType:
                AddLink("EntityStatistPage_Ship", baseInfo.ShipName);
                AddLink("EntityStatistPage_Class", baseInfo.ClassName);
                break;
            case IdName.CategoryEnum.SolarSystem:
                AddLink("EntityStatistPage_System", baseInfo.SystemName);
                AddLink("EntityStatistPage_Region", baseInfo.RegionName);
                AddPlain("EntityStatistPage_Sec", baseInfo.Sec?.ToString("0.0"));
                break;
            case IdName.CategoryEnum.Region:
                AddPlain("EntityStatistPage_Region", baseInfo.Name?.Name);
                break;
        }
    }

    private void OnStatisticChanged()
    {
        OnPropertyChanged(nameof(DangerRatio));
        OnPropertyChanged(nameof(GangRatio));
        OnPropertyChanged(nameof(ItemDestroyed));
        OnPropertyChanged(nameof(ItemLost));
        OnPropertyChanged(nameof(PointsDestroyed));
        OnPropertyChanged(nameof(PointsLost));
        OnPropertyChanged(nameof(SoloKills));
        OnPropertyChanged(nameof(SoloLosses));
        OnPropertyChanged(nameof(IskDestroyedText));
        OnPropertyChanged(nameof(IskLostText));
    }

    private static void Fill<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static string ModifierLabel(int index) => FindString(index switch
    {
        1 => "StatistKBListPage_Kills",
        2 => "StatistKBListPage_Losses",
        3 => "StatistKBListPage_Wormhole",
        4 => "StatistKBListPage_Solo",
        5 => "StatistKBListPage_Finalblow",
        6 => "StatistKBListPage_Awox",
        7 => "StatistKBListPage_Npc",
        _ => "StatistKBListPage_All",
    });

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>过滤类型下拉项。</summary>
    public sealed record ModifierOption(TypeModifier? Modifier, string Label);

    /// <summary>
    /// 信息卡的一行。<see cref="Link"/> 非空时界面把值渲染成链接样式（点击打开该实体的统计标签），
    /// 否则为纯文本（成员数/安全等级等）。
    /// </summary>
    public sealed record EntityInfoRow(string Label, string Value, IdName? Link = null)
    {
        public bool HasLink => Link is not null;
    }
}
