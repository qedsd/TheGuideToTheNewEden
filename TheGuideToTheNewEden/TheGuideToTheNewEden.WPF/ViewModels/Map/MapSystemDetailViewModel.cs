using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using TheGuideToTheNewEden.Core.Models.PlanetResources;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Models.Wormhole;
using TheGuideToTheNewEden.WPF.Services.KB;
using TheGuideToTheNewEden.WPF.Views.UserControls.Map;
using ZKB.NET;
using ZKB.NET.Models.KillStream;

namespace TheGuideToTheNewEden.WPF.ViewModels.Map;

/// <summary>设施升级行（带"当前星系资源是否够用"标记）。</summary>
public sealed class UpgradeRow
{
    private readonly long _systemPower;
    private readonly long _systemWorkforce;
    private readonly long _systemSuperionicIce;
    private readonly long _systemMagmaticGas;

    public UpgradeRow(
        Upgrade upgrade,
        long systemPower = 0,
        long systemWorkforce = 0,
        long systemSuperionicIce = 0,
        long systemMagmaticGas = 0)
    {
        Upgrade = upgrade;
        _systemPower = systemPower;
        _systemWorkforce = systemWorkforce;
        _systemSuperionicIce = systemSuperionicIce;
        _systemMagmaticGas = systemMagmaticGas;
    }

    public Upgrade Upgrade { get; }
    public string Name => Upgrade.Name ?? Upgrade.Id.ToString();
    public long Power => Upgrade.Power;
    public long Workforce => Upgrade.Workforce;
    public long SuperionicIce => Upgrade.SuperionicIce;
    public long MagmaticGas => Upgrade.MagmaticGas;

    /// <summary>该星系四项资源是否都满足此项设施要求。</summary>
    public bool Fit =>
        Upgrade.Power <= _systemPower
        && Upgrade.Workforce <= _systemWorkforce
        && Upgrade.SuperionicIce <= _systemSuperionicIce
        && Upgrade.MagmaticGas <= _systemMagmaticGas;
}

/// <summary>星系详情里的行星资源行。</summary>
public sealed class SystemPlanetRow
{
    public string PlanetName { get; init; } = string.Empty;
    public int PlanetId { get; init; }
    public long Power { get; init; }
    public long Workforce { get; init; }
    public long MagmaticGas { get; init; }
    public long SuperionicIce { get; init; }
}

/// <summary>星系详情里的天体行。</summary>
public sealed class CelestialRow
{
    public string Name { get; init; } = string.Empty;
    public int ItemId { get; init; }
    public string TypeName { get; init; } = string.Empty;
}

/// <summary>星系详情里的邻接星系行。</summary>
public sealed class NeighborRow
{
    public int SystemId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string RegionName { get; init; } = string.Empty;
    public string SecurityText { get; init; } = string.Empty;
    public string SovName { get; init; } = string.Empty;
}

/// <summary>
/// 星系详情（对齐 WinUI <c>MapSystemDetailPage</c>）：左侧固定信息栏 + 四个顶层页签；行星资源内含支持设施 / 不支持设施 / 详细。
/// （统计 / 设施升级 / 行星资源 / 天体 / 邻接）。
/// 数据来源：本地库（行星资源、天体、邻接）+ ESI 统计（<see cref="MapPageViewModel.GetSystemStat"/>）+ 主权缓存 + ZKB 系统击杀统计。
/// </summary>
public sealed class MapSystemDetailViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly SKColor[] ZkbDayColors =
    [
        new(0x56, 0xB4, 0xE9),
        new(0x00, 0x9E, 0x73),
        new(0xE6, 0x9F, 0x00),
        new(0xCC, 0x79, 0xA7),
        new(0x00, 0x72, 0xB2),
        new(0xD5, 0x5E, 0x00),
        new(0x8B, 0x86, 0x80),
    ];

    private static readonly string[] ZkbDayKeys =
    [
        "WormholePage_ZKB_DayOfWeek1",
        "WormholePage_ZKB_DayOfWeek2",
        "WormholePage_ZKB_DayOfWeek3",
        "WormholePage_ZKB_DayOfWeek4",
        "WormholePage_ZKB_DayOfWeek5",
        "WormholePage_ZKB_DayOfWeek6",
        "WormholePage_ZKB_DayOfWeek7",
    ];

    private readonly MapPageViewModel _map;
    private readonly Axis _zkbXAxis;
    private readonly Axis _zkbYAxis;
    private readonly CancellationTokenSource _zkbCts = new();
    private bool _disposed;

    private bool _isLoading;
    private bool _loaded;

    public ISeries[] ZkbActivitySeries { get; private set; } = [];
    public Axis[] ZkbXAxes { get; }
    public Axis[] ZkbYAxes { get; }

    public MapSystemDetailViewModel(MapSystemNode node, MapPageViewModel map)
    {
        Node = node;
        _map = map;
        _zkbXAxis = new Axis { TextSize = 11, MinLimit = -0.5, MaxLimit = 23.5 };
        _zkbYAxis = new Axis { TextSize = 11, MinLimit = 0, Labeler = v => ((int)v).ToString() };
        ZkbXAxes = [_zkbXAxis];
        ZkbYAxes = [_zkbYAxis];
        SystemIdText = node.Id.ToString();
        Name = node.Name;
        SecurityText = Helpers.MapTextHelper.FormatSecurity(node.Security, 2);
        RegionName = node.RegionName;
        SovName = Services.Map.SovService.GetSovName(node.Id);
        var resources = Services.Map.MapResourceService.SystemResources.TryGetValue(node.Id, out var res) ? res : null;
        Power = Services.Map.MapResourceService.GetValue(resources, Services.Map.ResourceKind.Power);
        Workforce = Services.Map.MapResourceService.GetValue(resources, Services.Map.ResourceKind.Workforce);
        MagmaticGas = Services.Map.MapResourceService.GetValue(resources, Services.Map.ResourceKind.MagmaticGas);
        SuperionicIce = Services.Map.MapResourceService.GetValue(resources, Services.Map.ResourceKind.SuperionicIce);
    }

    public MapSystemNode Node { get; }

    public string Name { get; }
    public string SystemIdText { get; }
    public string SecurityText { get; }
    public string RegionName { get; }
    public string SovName { get; }
    public bool HasSov => !string.IsNullOrEmpty(SovName);

    private long _power;
    private long _workforce;
    private long _magmaticGas;
    private long _superionicIce;

    public long Power
    {
        get => _power;
        private set => Set(ref _power, value);
    }

    public long Workforce
    {
        get => _workforce;
        private set => Set(ref _workforce, value);
    }

    public long MagmaticGas
    {
        get => _magmaticGas;
        private set => Set(ref _magmaticGas, value);
    }

    public long SuperionicIce
    {
        get => _superionicIce;
        private set => Set(ref _superionicIce, value);
    }

    private long _shipKills;
    private long _npcKills;
    private long _podKills;
    private long _jumps;
    private bool _hasZkbData;
    private string _zkbKillsText = "—";
    private string _zkbIskText = "—";
    private string _zkbSpanText = "—";
    private bool _isZkbLoading;

    public string ShipKillsText => _shipKills.ToString("N0");
    public string NpcKillsText => _npcKills.ToString("N0");
    public string PodKillsText => _podKills.ToString("N0");
    public string JumpsText => _jumps.ToString("N0");

    public bool IsZkbLoading
    {
        get => _isZkbLoading;
        private set => Set(ref _isZkbLoading, value);
    }

    public bool ZkbHasData
    {
        get => _hasZkbData;
        private set => Set(ref _hasZkbData, value);
    }

    public string ZkbKillsText => _zkbKillsText;
    public string ZkbIskText => _zkbIskText;
    public string ZkbSpanText => _zkbSpanText;

    public string ZkbStatusText => IsZkbLoading
        ? FindString("MapTool_Detail_ZkbLoading")
        : ZkbHasData
            ? string.Empty
            : FindString("MapTool_Detail_ZkbNoData");

    public ObservableCollection<SystemPlanetRow> PlanetRows { get; } = [];
    public ObservableCollection<UpgradeRow> SupportedUpgradeRows { get; } = [];
    public ObservableCollection<UpgradeRow> UnsupportedUpgradeRows { get; } = [];
    public ObservableCollection<ActiveEntityItem> ZkbTopCharacters { get; } = [];
    public ObservableCollection<ActiveEntityItem> ZkbTopCorporations { get; } = [];
    public ObservableCollection<ActiveEntityItem> ZkbTopAlliances { get; } = [];
    public ObservableCollection<ShipTypeStatItem> ZkbTopShips { get; } = [];
    public ObservableCollection<CelestialRow> CelestialRows { get; } = [];
    public ObservableCollection<NeighborRow> NeighborRows { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set => Set(ref _isLoading, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>装载详情页数据（幂等）。</summary>
    public async Task LoadAsync()
    {
        if (_loaded || IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var stat = _map.GetSystemStat(Node.Id);
            _shipKills = stat?.ShipKills ?? 0;
            _npcKills = stat?.NpcKills ?? 0;
            _podKills = stat?.PodKills ?? 0;
            _jumps = stat?.Jumps ?? 0;
            OnPropertyChanged(nameof(ShipKillsText));
            OnPropertyChanged(nameof(NpcKillsText));
            OnPropertyChanged(nameof(PodKillsText));
            OnPropertyChanged(nameof(JumpsText));

            // 资源清单还没装载过（没开过行星资源着色/清单页）时，按单个星系补一次，避免详情页四项资源全 0。
            // 单独 try/catch：Core 这一路依赖本地库，万一异常也只丢"四项资源"，不能让详情页全都加载不出来。
            if (!Services.Map.MapResourceService.IsLoaded && _power + _workforce + _magmaticGas + _superionicIce == 0)
            {
                try
                {
                    var aggregate = await Task.Run(() => Core.Services.DB.SolarSystemResourcesService.QueryBySolarSystemID(Node.Id));
                    if (aggregate is not null)
                    {
                        Power = aggregate.Power;
                        Workforce = aggregate.Workforce;
                        MagmaticGas = aggregate.MagmaticGas;
                        SuperionicIce = aggregate.SuperionicIce;
                    }
                }
                catch (Exception ex)
                {
                    Core.Log.Error(ex);
                }
            }

            var systemId = Node.Id;
            var details = await Task.Run(() => Services.Map.MapResourceService.GetSystemDetails(systemId));
            var celestials = await Task.Run(() => Services.Map.MapResourceService.GetCelestials(systemId));

            PlanetRows.Clear();
            foreach (var detail in details.Where(p => p.ContainResource))
            {
                PlanetRows.Add(new SystemPlanetRow
                {
                    PlanetName = detail.MapDenormalize?.ItemName ?? string.Empty,
                    PlanetId = detail.MapDenormalize?.ItemID ?? 0,
                    Power = detail.PlanetResources?.Power ?? 0,
                    Workforce = detail.PlanetResources?.Workforce ?? 0,
                    MagmaticGas = detail.MagmaticGas,
                    SuperionicIce = detail.SuperionicIce,
                });
            }

            CelestialRows.Clear();
            foreach (var celestial in celestials)
            {
                CelestialRows.Add(new CelestialRow
                {
                    Name = celestial.ItemName ?? string.Empty,
                    ItemId = celestial.ItemID,
                    TypeName = celestial.Type?.TypeName ?? string.Empty,
                });
            }

            SupportedUpgradeRows.Clear();
            UnsupportedUpgradeRows.Clear();
            foreach (var upgrade in Services.Map.MapResourceService.GetUpgrades()
                .OrderBy(p => p.Name ?? p.Id.ToString(), StringComparer.CurrentCultureIgnoreCase))
            {
                var row = new UpgradeRow(upgrade, Power, Workforce, SuperionicIce, MagmaticGas);
                (row.Fit ? SupportedUpgradeRows : UnsupportedUpgradeRows).Add(row);
            }

            BuildNeighbors();
            _loaded = true;
            _ = LoadZkbAsync();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadZkbAsync()
    {
        var token = _zkbCts.Token;
        if (token.IsCancellationRequested)
        {
            return;
        }

        IsZkbLoading = true;
        NotifyZkbChanged();
        try
        {
            var details = await FetchZkbDetailsAsync(token);
            token.ThrowIfCancellationRequested();
            if (details.Count == 0)
            {
                ZkbHasData = false;
                _zkbKillsText = "—";
                _zkbIskText = "—";
                _zkbSpanText = "—";
                return;
            }

            var analysis = await BuildZkbStatisticsAsync(details, token);
            token.ThrowIfCancellationRequested();
            ApplyZkbStatistics(analysis);
        }
        catch (OperationCanceledException)
        {
            // 窗口已关闭：不再回写任何 UI 状态。
            return;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            ZkbHasData = false;
            _zkbKillsText = "—";
            _zkbIskText = "—";
            _zkbSpanText = "—";
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsZkbLoading = false;
                NotifyZkbChanged();
            }
        }
    }

    /// <summary>最近两页 ZKB 击杀（最多 400 条），与虫洞同口径。</summary>
    private async Task<List<SKBDetail>> FetchZkbDetailsAsync(CancellationToken token)
    {
        var details = new List<SKBDetail>();
        for (var page = 1; page <= 2; page++)
        {
            token.ThrowIfCancellationRequested();
            var modifiers = new[]
            {
                new ParamModifierData(ParamModifier.SystemID, Node.Id.ToString()),
                new ParamModifierData(ParamModifier.Page, page.ToString()),
            };
            var batch = await ZKB.NET.ZKB.GetKillmailDetailsAsync(
                modifiers,
                Array.Empty<TypeModifier>(),
                token);
            token.ThrowIfCancellationRequested();
            if (batch is not { Count: > 0 })
            {
                break;
            }

            details.AddRange(batch);
            if (batch.Count < 200)
            {
                break;
            }
        }

        return details;
    }

    private async Task<ZkbStatistics> BuildZkbStatisticsAsync(List<SKBDetail> details, CancellationToken token)
    {
        var nowUtc = DateTime.UtcNow;
        var totalIsk = details.Sum(p => p.Zkb?.TotalValue ?? 0);
        var earliest = details.Min(p => p.KillmailTime);
        var spanDays = (nowUtc - earliest).TotalDays;

        var charCount = new Dictionary<int, int>();
        var charLast = new Dictionary<int, DateTime>();
        var corpCount = new Dictionary<int, int>();
        var corpLast = new Dictionary<int, DateTime>();
        var allianceCount = new Dictionary<int, int>();
        var allianceLast = new Dictionary<int, DateTime>();
        var shipCount = new Dictionary<int, int>();

        void TouchCharacter(int? id, DateTime time)
        {
            if (id is > 0)
            {
                charCount[id.Value] = charCount.TryGetValue(id.Value, out var c) ? c + 1 : 1;
                if (!charLast.TryGetValue(id.Value, out var last) || time > last)
                {
                    charLast[id.Value] = time;
                }
            }
        }

        void TouchCorp(int? id, DateTime time)
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

        void TouchAlliance(int? id, DateTime time)
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
                TouchCharacter(detail.Victim.CharacterId, time);
                TouchCorp(detail.Victim.CorporationId, time);
                TouchAlliance(detail.Victim.AllianceId, time);
                TouchShip(detail.Victim.ShipTypeId);
            }

            if (detail.Attackers is not null)
            {
                foreach (var attacker in detail.Attackers)
                {
                    TouchCharacter(attacker.CharacterId, time);
                    TouchCorp(attacker.CorporationId, time);
                    TouchAlliance(attacker.AllianceId, time);
                }
            }
        }

        token.ThrowIfCancellationRequested();

        var nameIds = charCount.Keys.Concat(corpCount.Keys).Concat(allianceCount.Keys).ToList();
        var names = await ZkbQueryService.ResolveNamesAsync(nameIds);

        List<ActiveEntityItem> BuildEntities(
            Dictionary<int, int> counts,
            Dictionary<int, DateTime> lasts,
            bool isAlliance,
            bool isCharacter = false) => counts
            .OrderByDescending(p => p.Value)
            .Take(3)
            .Select(p =>
            {
                names.TryGetValue(p.Key, out var idName);
                return new ActiveEntityItem
                {
                    EntityId = p.Key,
                    IsAlliance = isAlliance,
                    IsCharacter = isCharacter,
                    Name = idName?.Name ?? p.Key.ToString(),
                    ImageUrl = isCharacter
                        ? GameImageHelper.BuildCharacterPortraitUrl(p.Key, 64) ?? string.Empty
                        : isAlliance
                            ? GameImageHelper.BuildAllianceLogoUrl(p.Key, 64) ?? string.Empty
                            : GameImageHelper.BuildCorporationLogoUrl(p.Key, 64) ?? string.Empty,
                    Count = p.Value,
                    LastActiveDays = (nowUtc - lasts[p.Key]).TotalDays,
                };
            })
            .ToList();

        var shipTypeIds = shipCount.OrderByDescending(p => p.Value).Take(5).Select(p => p.Key).ToList();
        var shipTypes = shipTypeIds.Count > 0
            ? await Core.Services.DB.InvTypeService.QueryTypesAsync(shipTypeIds) ?? []
            : [];
        var shipTypeMap = shipTypes.ToDictionary(p => p.TypeID);
        var ships = shipCount
            .OrderByDescending(p => p.Value)
            .Take(5)
            .Select(p => new ShipTypeStatItem
            {
                TypeId = p.Key,
                Name = shipTypeMap.TryGetValue(p.Key, out var type) ? type.TypeName : p.Key.ToString(),
                ImageUrl = GameImageHelper.BuildTypeImageUrl(p.Key, 64) ?? string.Empty,
                Count = p.Value,
            })
            .ToList();

        var matrix = new int[7, 24];
        foreach (var detail in details)
        {
            var local = detail.KillmailTime.ToLocalTime();
            matrix[((int)local.DayOfWeek + 6) % 7, local.Hour]++;
        }

        return new ZkbStatistics(
            details.Count,
            spanDays,
            totalIsk,
            BuildEntities(charCount, charLast, isAlliance: false, isCharacter: true),
            BuildEntities(corpCount, corpLast, isAlliance: false),
            BuildEntities(allianceCount, allianceLast, isAlliance: true),
            ships,
            matrix);
    }

    private void ApplyZkbStatistics(ZkbStatistics analysis)
    {
        ZkbTopCharacters.Clear();
        foreach (var item in analysis.Characters)
        {
            ZkbTopCharacters.Add(item);
        }

        ZkbTopCorporations.Clear();
        foreach (var item in analysis.Corporations)
        {
            ZkbTopCorporations.Add(item);
        }

        ZkbTopAlliances.Clear();
        foreach (var item in analysis.Alliances)
        {
            ZkbTopAlliances.Add(item);
        }

        ZkbTopShips.Clear();
        foreach (var item in analysis.Ships)
        {
            ZkbTopShips.Add(item);
        }

        _zkbKillsText = analysis.TotalKills.ToString("N0");
        _zkbIskText = IskFormatHelper.Format(analysis.TotalIsk);
        _zkbSpanText = string.Format(
            FindString("WormholePage_ZKB_SpanDays"),
            analysis.SpanDays.ToString("N0"));
        ZkbHasData = true;
        BuildZkbActivitySeries(analysis.HourDayCounts);
    }

    private sealed record ZkbStatistics(
        int TotalKills,
        double SpanDays,
        double TotalIsk,
        List<ActiveEntityItem> Characters,
        List<ActiveEntityItem> Corporations,
        List<ActiveEntityItem> Alliances,
        List<ShipTypeStatItem> Ships,
        int[,] HourDayCounts);

    private void BuildZkbActivitySeries(int[,] matrix)
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
                Name = FindString(ZkbDayKeys[day]),
                Stroke = null,
                Fill = new SolidColorPaint(ZkbDayColors[day]),
                MaxBarWidth = 24,
            };
        }

        _zkbXAxis.Labels = Enumerable.Range(0, 24).Select(p => p.ToString()).ToArray();
        ZkbActivitySeries = series;
        OnPropertyChanged(nameof(ZkbActivitySeries));
    }

    private void NotifyZkbChanged()
    {
        OnPropertyChanged(nameof(IsZkbLoading));
        OnPropertyChanged(nameof(ZkbHasData));
        OnPropertyChanged(nameof(ZkbStatusText));
        OnPropertyChanged(nameof(ZkbKillsText));
        OnPropertyChanged(nameof(ZkbIskText));
        OnPropertyChanged(nameof(ZkbSpanText));
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    /// <summary>窗口关闭时取消 ZKB 统计 / 击杀列表请求，避免后台继续跑完再回写。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _zkbCts.Cancel();
    }

    private void BuildNeighbors()
    {
        NeighborRows.Clear();
        var ids = _map.GetNeighbors(Node.Id);
        if (ids.Count == 0)
        {
            return;
        }

        var systems = Core.Services.DB.MapSolarSystemService.Query([.. ids]);
        var regionDic = Core.Services.DB.MapRegionService.QueryAll().ToDictionary(p => p.RegionID, p => p.RegionName);
        foreach (var system in systems.OrderBy(p => p.SolarSystemName))
        {
            NeighborRows.Add(new NeighborRow
            {
                SystemId = system.SolarSystemID,
                Name = system.SolarSystemName ?? string.Empty,
                RegionName = regionDic.TryGetValue(system.RegionID, out var regionName) ? regionName : string.Empty,
                SecurityText = Helpers.MapTextHelper.FormatSecurity(system.Security, 2),
                SovName = Services.Map.SovService.GetSovName(system.SolarSystemID),
            });
        }
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
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
