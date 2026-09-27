using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Models.Wormhole;
using TheGuideToTheNewEden.WPF.Services.KB;
using ZKB.NET;
using ZKB.NET.Models.KillStream;

namespace TheGuideToTheNewEden.WPF.ViewModels;

/// <summary>
/// 虫洞页 VM：洞系搜索/筛选 + 洞系详情 + 洞口库 + zKillboard 活跃度分析。
/// 对应 WinUI 的 WormholeViewModel，ZKB 数据层换成 WPF 的批量解析风格（一次汇总 ID、批量取名）。
/// </summary>
public sealed class WormholeViewModel : INotifyPropertyChanged
{
    /// <summary>过洞计算器内置的代表舰船（从 SDE 查实际质量；typeId 是稳定的）。</summary>
    private static readonly int[] PortalRepresentativeTypeIds =
    [
        587,    // Rifter 护卫
        626,    // Catalyst 驱逐
        621,    // Stabber 巡洋
        623,    // Hurricane 战列巡洋
        648,    // Iteron Mark V 工业
        645,    // Dominix 战列
        19724,  // Moros 无畏
        23773,  // Archon 航母
        42125,  // Ninazu 强援
        23913,  // Wyvern 超级航母
        11567,  // Avatar 泰坦
    ];

    /// <summary>活动分布图系列色（Okabe-Ito 色盲友好色板，深浅主题都可读；Skia 画笔无法用 DynamicResource）。</summary>
    private static readonly SKColor[] DayColors =
    [
        new(0x56, 0xB4, 0xE9), // 周一
        new(0x00, 0x9E, 0x73), // 周二
        new(0xE6, 0x9F, 0x00), // 周三
        new(0xCC, 0x79, 0xA7), // 周四
        new(0x00, 0x72, 0xB2), // 周五
        new(0xD5, 0x5E, 0x00), // 周六
        new(0x8B, 0x86, 0x80), // 周日
    ];

    /// <summary>与 DayColors 对应的星期语言键（索引 = DayOfWeek：周日=0）。</summary>
    private static readonly string[] DayNameKeys =
    [
        "WormholePage_ZKB_DayOfWeek7", // 周日
        "WormholePage_ZKB_DayOfWeek1", // 周一
        "WormholePage_ZKB_DayOfWeek2",
        "WormholePage_ZKB_DayOfWeek3",
        "WormholePage_ZKB_DayOfWeek4",
        "WormholePage_ZKB_DayOfWeek5",
        "WormholePage_ZKB_DayOfWeek6",
    ];

    public WormholeViewModel()
    {
        _xAxis = new Axis { TextSize = 11, MinLimit = -0.5, MaxLimit = 23.5 };
        _yAxis = new Axis { TextSize = 11, MinLimit = 0, Labeler = v => ((int)v).ToString() };
        XAxes = [_xAxis];
        YAxes = [_yAxis];
    }

    // ==================================================================
    //  数据加载
    // ==================================================================

    private List<WormholeListItem> _allWormholes = [];
    private List<PortalRow> _allPortals = [];

    private bool _isListLoading;
    public bool IsListLoading { get => _isListLoading; private set => Set(ref _isListLoading, value); }

    /// <summary>
    /// 页面首次 Loaded 时加载全部静态数据（洞系 + 洞口库）。
    /// 注意：不加 ConfigureAwait —— 后面的 ObservableCollection 填充必须回到 UI 线程。
    /// </summary>
    public async Task LoadAsync()
    {
        if (_allWormholes.Count > 0 || IsListLoading)
        {
            return;
        }

        IsListLoading = true;
        try
        {
            var wormholes = await Task.Run(() => WormholeService.QueryWormholeAsync());
            _allWormholes = wormholes?
                .Where(p => !string.IsNullOrEmpty(p.Name))
                .OrderBy(p => p.Name)
                .Select(p => new WormholeListItem(p))
                .ToList() ?? [];

            var portals = await Task.Run(() => WormholeService.QueryPortalAsync());
            _allPortals = portals?
                .Where(p => !string.IsNullOrEmpty(p.Name))
                .OrderBy(p => p.Name)
                .Select(p => new PortalRow(p).WithDestinationCodes())
                .ToList() ?? [];

            ApplyWormholeFilter();
            ApplyPortalFilter();
        }
        catch (System.Exception ex)
        {
            Core.Log.Error(ex);
            Services.PageNotifyService.Error($"{FindString("WormholePage_LoadFailed")}: {ex.Message}");
        }
        finally
        {
            IsListLoading = false;
        }
    }

    // ==================================================================
    //  洞系搜索 / 筛选
    // ==================================================================

    public ObservableCollection<WormholeListItem> FilteredWormholes { get; } = [];

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value))
            {
                ApplyWormholeFilter();
            }
        }
    }

    /// <summary>等级筛选索引：0=全部，1-6=C1-C6，7-12=C12-C18。</summary>
    private int _classIndex;
    public int ClassIndex
    {
        get => _classIndex;
        set
        {
            if (Set(ref _classIndex, value))
            {
                ApplyWormholeFilter();
            }
        }
    }

    /// <summary>天象筛选索引：0=全部，1-6=天象代码 0-5。</summary>
    private int _phenomenaIndex;
    public int PhenomenaIndex
    {
        get => _phenomenaIndex;
        set
        {
            if (Set(ref _phenomenaIndex, value))
            {
                ApplyWormholeFilter();
            }
        }
    }

    public int FilteredCount => FilteredWormholes.Count;

    private void ApplyWormholeFilter()
    {
        var text = _searchText?.Trim() ?? string.Empty;
        int? wormholeClass = _classIndex > 0 ? ClassIndexToClass(_classIndex) : null;
        int? phenomena = _phenomenaIndex > 0 ? _phenomenaIndex - 1 : null;

        var query = _allWormholes.AsEnumerable();
        if (text.Length > 0)
        {
            query = query.Where(p => p.Name.Contains(text, System.StringComparison.OrdinalIgnoreCase));
        }
        if (wormholeClass is int wc)
        {
            query = query.Where(p => p.Class == wc);
        }
        if (phenomena is int ph)
        {
            query = query.Where(p => p.Phenomena == ph);
        }

        FilteredWormholes.Clear();
        foreach (var item in query)
        {
            FilteredWormholes.Add(item);
        }
        Raise(nameof(FilteredCount));
    }

    private static int ClassIndexToClass(int index) => index switch
    {
        >= 1 and <= 6 => index,      // C1-C6
        >= 7 and <= 12 => index + 5, // C12-C18
        _ => 0,
    };

    // ==================================================================
    //  洞系详情
    // ==================================================================

    private WormholeListItem? _selectedWormhole;
    public WormholeListItem? SelectedWormhole
    {
        get => _selectedWormhole;
        set
        {
            if (Set(ref _selectedWormhole, value))
            {
                _ = LoadDetailAsync(value);
            }
        }
    }

    private WormholeDetail? _detail;
    public WormholeDetail? Detail
    {
        get => _detail;
        private set
        {
            Set(ref _detail, value);
            if (value is not null)
            {
                _ = AnalyzeKbAsync(value.Id);
            }
        }
    }

    private int _detailRequestId;

    private async Task LoadDetailAsync(WormholeListItem? item)
    {
        if (item is null)
        {
            Detail = null;
            return;
        }

        var requestId = ++_detailRequestId;
        try
        {
            // 不加 ConfigureAwait：Detail 的 PropertyChanged 与随后启动的 ZKB 分析
            // 里的集合操作都要在 UI 线程上发生
            var detail = await Task.Run(() => BuildDetail(item.Wormhole));
            if (requestId == _detailRequestId)
            {
                Detail = detail;
            }
        }
        catch (System.Exception ex)
        {
            Core.Log.Error(ex);
            if (requestId == _detailRequestId)
            {
                Detail = null;
            }
        }
    }

    private static WormholeDetail BuildDetail(Wormhole wormhole)
    {
        List<WormholePortal> LoadPortals(string? ids)
        {
            var result = new List<WormholePortal>();
            if (string.IsNullOrWhiteSpace(ids))
            {
                return result;
            }

            foreach (var part in ids.Split(','))
            {
                if (!int.TryParse(part.Trim(), out var portalId))
                {
                    continue;
                }

                try
                {
                    var portal = WormholeService.QueryPortal(portalId);
                    if (portal is not null)
                    {
                        result.Add(portal);
                    }
                }
                catch (System.Exception ex)
                {
                    // 单个洞口缺失只丢该洞
                    Core.Log.Error(ex);
                }
            }
            return result;
        }

        // WinUI 口径：行星 GroupID 6/7、月球 8，其余归"其他"
        var stellars = MapDenormalizeService.QueryBySolarSystemID(wormhole.Id, true) ?? [];
        var planets = stellars
            .Where(p => p.GroupID is 6 or 7 && p.Type is not null)
            .Select(p => new StellarItem(p, p.Type!.TypeName))
            .ToList();
        var moons = stellars
            .Where(p => p.GroupID == 8 && p.Type is not null)
            .Select(p => new StellarItem(p, p.Type!.TypeName))
            .ToList();
        var others = stellars
            .Where(p => p.GroupID is not 6 and not 7 and not 8 && p.Type is not null)
            .Select(p => new StellarItem(p, p.Type!.TypeName))
            .ToList();

        return WormholeDetail.Create(
            wormhole,
            LoadPortals(wormhole.Statics),
            LoadPortals(wormhole.Wanderings),
            planets,
            moons,
            others);
    }

    // ==================================================================
    //  zKillboard 活跃度分析
    // ==================================================================

    private bool _isKbLoading;
    public bool IsKbLoading { get => _isKbLoading; private set => Set(ref _isKbLoading, value); }

    private string _kbSummary = string.Empty;
    public string KbSummary { get => _kbSummary; private set => Set(ref _kbSummary, value); }

    private bool _hasKbData;
    public bool HasKbData { get => _hasKbData; private set => Set(ref _hasKbData, value); }

    /// <summary>概要：击杀数 / ISK 损失 / 时间跨度（新增于 WinUI：三格概要）。</summary>
    public string KbKillsText { get; private set; } = string.Empty;
    public string KbIskText { get; private set; } = string.Empty;
    public string KbSpanText { get; private set; } = string.Empty;

    public ObservableCollection<ActiveEntityItem> TopCorporations { get; } = [];
    public ObservableCollection<ActiveEntityItem> TopAlliances { get; } = [];
    public ObservableCollection<ShipTypeStatItem> TopShips { get; } = [];

    /// <summary>分析范围索引：0-3 → 最近 200/400/600/800 条击杀。</summary>
    private int _kbRangeIndex = 1;
    public int KbRangeIndex
    {
        get => _kbRangeIndex;
        set
        {
            if (Set(ref _kbRangeIndex, value) && _detail is not null && !_isKbLoading)
            {
                _ = AnalyzeKbAsync(_detail.Id);
            }
        }
    }

    public ISeries[] ActivitySeries { get; private set; } = [];
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }
    private readonly Axis _xAxis;
    private readonly Axis _yAxis;

    private CancellationTokenSource? _kbCts;

    public void RefreshKb()
    {
        if (_detail is not null && !_isKbLoading)
        {
            _ = AnalyzeKbAsync(_detail.Id);
        }
    }

    private async Task AnalyzeKbAsync(int wormholeId)
    {
        _kbCts?.Cancel();
        var cts = new CancellationTokenSource();
        _kbCts = cts;
        var token = cts.Token;

        IsKbLoading = true;
        HasKbData = false;
        KbSummary = string.Empty;
        TopCorporations.Clear();
        TopAlliances.Clear();
        TopShips.Clear();
        ActivitySeries = [];
        Raise(nameof(ActivitySeries));

        try
        {
            var pages = System.Math.Clamp(_kbRangeIndex + 1, 1, 4);
            var details = new List<SKBDetail>();
            for (var page = 1; page <= pages; page++)
            {
                token.ThrowIfCancellationRequested();
                var modifiers = new[]
                {
                    new ParamModifierData(ParamModifier.SystemID, wormholeId.ToString()),
                    new ParamModifierData(ParamModifier.Page, page.ToString()),
                };

                var batch = await GetKillDetailsWithRetryAsync(modifiers, token);
                if (batch is not { Count: > 0 })
                {
                    break;
                }

                details.AddRange(batch);
                if (batch.Count < 200)
                {
                    break; // 没有更多页
                }
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (details.Count == 0)
            {
                KbSummary = FindString("WormholePage_ZKB_NoData");
                return;
            }

            // 统计在后台算，但结果回 UI 线程后再动集合（不加 ConfigureAwait）
            var analysis = await Task.Run(() => BuildStatisticsAsync(details, token), token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            TopCorporations.Clear();
            foreach (var item in analysis.Corporations)
            {
                TopCorporations.Add(item);
            }
            TopAlliances.Clear();
            foreach (var item in analysis.Alliances)
            {
                TopAlliances.Add(item);
            }
            TopShips.Clear();
            foreach (var item in analysis.Ships)
            {
                TopShips.Add(item);
            }

            BuildActivitySeries(analysis.HourDayCounts);
            HasKbData = true;
            KbKillsText = analysis.TotalKills.ToString("N0");
            KbIskText = Helpers.IskFormatHelper.Format(analysis.TotalIsk);
            KbSpanText = string.Format(FindString("WormholePage_ZKB_SpanDays"), analysis.SpanDays.ToString("N0"));
            KbSummary = string.Format(
                FindString("WormholePage_ZKB_DataFrom"),
                analysis.SpanDays.ToString("N0"),
                analysis.TotalKills.ToString("N0"));
        }
        catch (OperationCanceledException)
        {
            // 换了星系或手动刷新：静默
        }
        catch (System.Exception ex)
        {
            Core.Log.Error(ex);
            Services.PageNotifyService.Error($"{FindString("WormholePage_ZKB_Failed")}: {ex.Message}");
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsKbLoading = false;
            }
        }
    }

    /// <summary>直取 zKB 完整 killmail；偶发失败重试一次（与 ZkbQueryService 的约定一致）。</summary>
    private static async Task<List<SKBDetail>?> GetKillDetailsWithRetryAsync(ParamModifierData[] modifiers, CancellationToken token)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                var result = await ZKB.NET.ZKB.GetKillmailDetailsAsync(modifiers, Array.Empty<TypeModifier>()).ConfigureAwait(false);
                if (result is { Count: > 0 })
                {
                    return result;
                }

                if (attempt == 1)
                {
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (System.Exception ex)
            {
                Core.Log.Warn($"[Wormhole ZKB] kills 请求失败（第 {attempt + 1} 次）：{ex.Message}");
                if (attempt == 1)
                {
                    return null;
                }
            }
        }

        return null;
    }

    private sealed record KbStatistics(
        int TotalKills,
        double SpanDays,
        double TotalIsk,
        List<ActiveEntityItem> Corporations,
        List<ActiveEntityItem> Alliances,
        List<ShipTypeStatItem> Ships,
        int[,] HourDayCounts);

    private static async Task<KbStatistics> BuildStatisticsAsync(List<SKBDetail> details, CancellationToken token)
    {
        var nowUtc = System.DateTime.UtcNow;
        var totalIsk = details.Sum(p => p.Zkb?.TotalValue ?? 0);
        var earliest = details.Min(p => p.KillmailTime);
        var spanDays = (nowUtc - earliest).TotalDays;

        // 军团/联盟活跃度：受害方 + 全部攻击方各计一次（与 WinUI 口径一致）
        var corpCount = new Dictionary<int, int>();
        var corpLast = new Dictionary<int, System.DateTime>();
        var allianceCount = new Dictionary<int, int>();
        var allianceLast = new Dictionary<int, System.DateTime>();
        var shipCount = new Dictionary<int, int>();

        void TouchCorp(int? id, System.DateTime time)
        {
            if (id is > 0)
            {
                corpCount[id.Value] = corpCount.TryGetValue(id.Value, out var c) ? c + 1 : 1;
                if (!corpLast.TryGetValue(id.Value, out var last) || time > last)
                {
                    corpLast[id.Value] = time;
                }
            }
        }

        void TouchAlliance(int? id, System.DateTime time)
        {
            if (id is > 0)
            {
                allianceCount[id.Value] = allianceCount.TryGetValue(id.Value, out var c) ? c + 1 : 1;
                if (!allianceLast.TryGetValue(id.Value, out var last) || time > last)
                {
                    allianceLast[id.Value] = time;
                }
            }
        }

        void TouchShip(int? id)
        {
            if (id is > 0)
            {
                shipCount[id.Value] = shipCount.TryGetValue(id.Value, out var c) ? c + 1 : 1;
            }
        }

        foreach (var detail in details)
        {
            var time = detail.KillmailTime;
            if (detail.Victim is not null)
            {
                TouchCorp(detail.Victim.CorporationId, time);
                TouchAlliance(detail.Victim.AllianceId, time);
                TouchShip(detail.Victim.ShipTypeId);
            }

            if (detail.Attackers is not null)
            {
                foreach (var attacker in detail.Attackers)
                {
                    TouchCorp(attacker.CorporationId, time);
                    TouchAlliance(attacker.AllianceId, time);
                }
            }
        }

        token.ThrowIfCancellationRequested();

        // 名称批量解析（本地库 + ESI）
        var nameIds = corpCount.Keys.Concat(allianceCount.Keys).ToList();
        var names = await ZkbQueryService.ResolveNamesAsync(nameIds).ConfigureAwait(false);

        List<ActiveEntityItem> BuildEntities(Dictionary<int, int> counts, Dictionary<int, System.DateTime> lasts, bool isAlliance) => counts
            .OrderByDescending(p => p.Value)
            .Take(3)
            .Select(p =>
            {
                names.TryGetValue(p.Key, out var idName);
                return new ActiveEntityItem
                {
                    EntityId = p.Key,
                    IsAlliance = isAlliance,
                    Name = idName?.Name ?? p.Key.ToString(),
                    ImageUrl = isAlliance
                        ? Helpers.GameImageHelper.BuildAllianceLogoUrl(p.Key, 64) ?? string.Empty
                        : Helpers.GameImageHelper.BuildCorporationLogoUrl(p.Key, 64) ?? string.Empty,
                    Count = p.Value,
                    LastActiveDays = (nowUtc - lasts[p.Key]).TotalDays,
                };
            })
            .ToList();

        // 被击毁船型 Top5
        var shipTypeIds = shipCount.OrderByDescending(p => p.Value).Take(5).Select(p => p.Key).ToList();
        var shipTypes = shipTypeIds.Count > 0
            ? await InvTypeService.QueryTypesAsync(shipTypeIds).ConfigureAwait(false) ?? []
            : [];
        var shipTypeMap = shipTypes.ToDictionary(p => p.TypeID);
        var ships = shipCount
            .OrderByDescending(p => p.Value)
            .Take(5)
            .Select(p => new ShipTypeStatItem
            {
                TypeId = p.Key,
                Name = shipTypeMap.TryGetValue(p.Key, out var type) ? type.TypeName : p.Key.ToString(),
                ImageUrl = Helpers.GameImageHelper.BuildTypeImageUrl(p.Key, 64) ?? string.Empty,
                Count = p.Value,
            })
            .ToList();

        // 本地小时 × 星期计数矩阵（行=星期 0-6 周一..周日，列=小时 0-23）
        var matrix = new int[7, 24];
        foreach (var detail in details)
        {
            var local = detail.KillmailTime.ToLocalTime();
            var dayIndex = ((int)local.DayOfWeek + 6) % 7;
            matrix[dayIndex, local.Hour]++;
        }

        return new KbStatistics(
            details.Count,
            spanDays,
            totalIsk,
            BuildEntities(corpCount, corpLast, isAlliance: false),
            BuildEntities(allianceCount, allianceLast, isAlliance: true),
            ships,
            matrix);
    }

    private void BuildActivitySeries(int[,] matrix)
    {
        var series = new ISeries[7];
        for (var day = 0; day < 7; day++)
        {
            var values = new double[24];
            for (var hour = 0; hour < 24; hour++)
            {
                values[hour] = matrix[day, hour];
            }

            series[day] = new StackedColumnSeries<double>
            {
                Values = values,
                Name = FindString(DayNameKeys[day]),
                Stroke = null,
                Fill = new SolidColorPaint(DayColors[day]),
                MaxBarWidth = 24,
            };
        }

        _xAxis.Labels = Enumerable.Range(0, 24).Select(p => p.ToString()).ToArray();

        ActivitySeries = series;
        Raise(nameof(ActivitySeries));
    }

    // ==================================================================
    //  洞口库
    // ==================================================================

    public ObservableCollection<PortalRow> FilteredPortals { get; } = [];

    private string _portalSearchText = string.Empty;
    public string PortalSearchText
    {
        get => _portalSearchText;
        set
        {
            if (Set(ref _portalSearchText, value))
            {
                ApplyPortalFilter();
            }
        }
    }

    /// <summary>通往空间筛选：0=全部，1=高安，2=低安，3=00，4-9=C1-C6，10=C12(希拉)，11=C13(破碎)。</summary>
    private int _portalDestIndex;
    public int PortalDestIndex
    {
        get => _portalDestIndex;
        set
        {
            if (Set(ref _portalDestIndex, value))
            {
                ApplyPortalFilter();
            }
        }
    }

    /// <summary>再生筛选：0=全部，1=固定，2=随机。</summary>
    private int _portalRespawnIndex;
    public int PortalRespawnIndex
    {
        get => _portalRespawnIndex;
        set
        {
            if (Set(ref _portalRespawnIndex, value))
            {
                ApplyPortalFilter();
            }
        }
    }

    public int PortalCount => FilteredPortals.Count;

    private void ApplyPortalFilter()
    {
        var text = _portalSearchText?.Trim() ?? string.Empty;
        int? dest = _portalDestIndex > 0 ? PortalDestIndexToCode(_portalDestIndex) : null;
        string? respawn = _portalRespawnIndex switch
        {
            1 => "Static",
            2 => "Wandering",
            _ => null,
        };

        var query = _allPortals.AsEnumerable();
        if (text.Length > 0)
        {
            query = query.Where(p => p.Name.Contains(text, System.StringComparison.OrdinalIgnoreCase));
        }
        if (dest is int code)
        {
            query = query.Where(p => p.DestinationCodes.Contains(code));
        }
        if (respawn is string r)
        {
            query = query.Where(p => string.Equals(p.Portal.Respawn, r, System.StringComparison.OrdinalIgnoreCase));
        }

        FilteredPortals.Clear();
        foreach (var row in query)
        {
            FilteredPortals.Add(row);
        }
        Raise(nameof(PortalCount));
    }

    private static int PortalDestIndexToCode(int index) => index switch
    {
        1 => 100,                   // 高安
        2 => 101,                   // 低安
        3 => 102,                   // 00
        >= 4 and <= 9 => index - 3, // C1-C6
        10 => 12,                   // 希拉
        11 => 13,                   // 破碎
        _ => 0,
    };

    // ==================================================================
    //  过洞计算器船型选项
    // ==================================================================

    public List<ShipMassOption> ShipOptions { get; private set; } = [];

    /// <summary>加载内置代表舰船的精确质量（SDE types.mass）。</summary>
    public async Task LoadShipOptionsAsync()
    {
        if (ShipOptions.Count > 0)
        {
            return;
        }

        try
        {
            var types = await InvTypeService.QueryTypeMassAsync(PortalRepresentativeTypeIds.ToList()).ConfigureAwait(false);
            if (types is null || types.Count == 0)
            {
                return;
            }

            var categoryKeys = new (int typeId, string key)[]
            {
                (587, "Frigate"),
                (626, "Destroyer"),
                (621, "Cruiser"),
                (623, "Battlecruiser"),
                (648, "Industrial"),
                (645, "Battleship"),
                (19724, "Dreadnought"),
                (23773, "Carrier"),
                (42125, "ForceAux"),
                (23913, "Supercarrier"),
                (11567, "Titan"),
            };
            var map = types.ToDictionary(p => p.TypeID);
            ShipOptions = categoryKeys
                .Where(p => map.ContainsKey(p.typeId) && map[p.typeId].Mass > 0)
                .Select(p => new ShipMassOption
                {
                    TypeId = p.typeId,
                    Name = map[p.typeId].TypeName ?? p.typeId.ToString(),
                    Category = FindString($"WormholePage_ShipClass_{p.key}"),
                    Mass = map[p.typeId].Mass,
                })
                .ToList();
            Raise(nameof(ShipOptions));
        }
        catch (System.Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    // ==================================================================
    //  工具
    // ==================================================================

    /// <summary>友链站点名（点击后由页面 code-behind 拼对应网址）。</summary>
    public IReadOnlyList<string> LinkNames { get; } = ["Anoik", "Dotlan", "Ellatha", "Zkillboard", "Chruker"];

    internal static string FindString(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(propertyName);
        return true;
    }
}
