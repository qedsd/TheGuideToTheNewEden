using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using SkiaSharp;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.Services.Map;
using TheGuideToTheNewEden.WPF.Views.UserControls.Map;

namespace TheGuideToTheNewEden.WPF.ViewModels.Map;

/// <summary>情报流条目（频道情报文本 + ZKB 击杀摘要）。</summary>
public sealed class IntelMsgItem
{
    public DateTime TimeUtc { get; init; }
    public string TimeText { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public int SystemId { get; init; }
    public string SystemName { get; init; } = string.Empty;
    public string Listener { get; init; } = string.Empty;
    public bool IsClear { get; init; }

    /// <summary>来源：频道情报 / ZKB 击杀（用于左侧色标）。</summary>
    public bool IsZkb { get; init; }
}

/// <summary>导航结果行。</summary>
public sealed class NavResultItem
{
    public int Index { get; init; }
    public MapSystemNode Node { get; init; } = null!;
    public string SecurityText { get; init; } = string.Empty;
    public string RegionName { get; init; } = string.Empty;
    /// <summary>距上一跳距离（光年）。</summary>
    public double DistanceLy { get; init; }
    /// <summary>0 起点 / 1 星门 / 2 旗舰跳 / 3 跳桥。</summary>
    public int NavType { get; init; }
    public string NavTypeText { get; init; } = string.Empty;
    /// <summary>本跳预计燃料（仅旗舰跳有值）。</summary>
    public double Fuel { get; init; }
    public string FuelText => Fuel > 0 ? Fuel.ToString("N2") : string.Empty;
}

/// <summary>星系 ESI 统计（详情页与信息卡共用）。</summary>
public sealed class SystemStatInfo
{
    public long ShipKills { get; init; }
    public long NpcKills { get; init; }
    public long PodKills { get; init; }
    public long Jumps { get; init; }
}

/// <summary>
/// 星图页 ViewModel：数据装载（星系/星门/ESI 统计）、搜索、着色模式（安等 / 主权 / 行星资源 / 击杀 / 通行）、
/// 跳桥层、情报模式（频道情报 + ZKB 击杀聚合）、角色标记、导航计算（星门 / 旗舰跳 / 跳桥 + 燃料）。
/// 画布交互桥接由 MapPage 代码后置完成。
/// </summary>
public sealed class MapPageViewModel : INotifyPropertyChanged
{
    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;

    /// <summary>情报消息上限（超出裁剪最旧的）。</summary>
    private const int MaxIntelMsgs = 1000;

    /// <summary>逐个设置自动航点之间的间隔（毫秒）——ESI 连续调用容易被限流。</summary>
    private const int AutopilotIntervalMs = 120;

    /// <summary>着色下拉的项顺序（UI 索引 → 模式，显式映射，避免 SelectedIndex 强转）。</summary>
    public static readonly MapColorMode[] ColorModes =
    [
        MapColorMode.Security,
        MapColorMode.Sovereignty,
        MapColorMode.PlanetResource,
        MapColorMode.Kills,
        MapColorMode.Jumps,
    ];

    /// <summary>行星资源子下拉的项顺序。</summary>
    public static readonly ResourceKind[] ResourceKinds =
    [
        ResourceKind.Power,
        ResourceKind.Workforce,
        ResourceKind.MagmaticGas,
        ResourceKind.SuperionicIce,
    ];

    private bool _isLoading;
    private bool _loaded;
    private string _searchText = string.Empty;
    private MapColorMode _colorMode = MapColorMode.Security;
    private ResourceKind _resourceKind = ResourceKind.Power;
    private bool _showCharacters;
    private bool _showBridges = true;
    private bool _intelRunning;
    private bool _zkbIntel;
    private double _zkbDurationMinutes = 20;
    private MapSystemNode? _selectedSystem;
    private double _selectedKills;
    private double _selectedJumps;
    private double _maxLy = 6;
    private bool _capitalMode;
    private bool _useGates = true;
    private bool _preferCheaper;
    private CapitalJumpShipInfo? _jumpShip;
    private int _jdc = 4;
    private int _jfc = 4;
    private int _jumpFreighters = 4;
    private AuthorizedCharacterData? _autopilotCharacter;
    private string _intelExcludeKeywords = string.Empty;
    private string _intelIncludeKeywords = string.Empty;
    private string _navSummary = string.Empty;

    private Dictionary<int, double> _killValues = [];
    private Dictionary<int, double> _jumpValues = [];
    private Dictionary<int, SystemStatInfo> _systemStats = [];
    private Dictionary<int, MapSystemNode> _nodeById = [];
    private List<MapSolarSystem> _systems = [];
    private double _killMax;
    private double _jumpMax;
    private double _resourceMax;
    private DispatcherTimer? _intelExpiryTimer;

    /// <summary>搜索建议结果。</summary>
    public ObservableCollection<MapSystemNode> SearchResults { get; } = [];

    public ObservableCollection<MapSystemNode> Waypoints { get; } = [];
    public ObservableCollection<MapSystemNode> AvoidSystems { get; } = [];
    public ObservableCollection<NavResultItem> NavResult { get; } = [];
    public ObservableCollection<IntelMsgItem> IntelMessages { get; } = [];
    public ObservableCollection<MapSystemNode> Neighbors { get; } = [];
    public ObservableCollection<AuthorizedCharacterData> AutopilotCharacters { get; } = [];
    public ObservableCollection<MapRegion> Regions { get; } = [];
    public ObservableCollection<string> ActiveListeners { get; } = [];
    public ObservableCollection<JumpBridge> Bridges { get; } = [];
    public ObservableCollection<CapitalJumpShipInfo> JumpShips { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set => Set(ref _isLoading, value);
    }

    public bool IsLoaded
    {
        get => _loaded;
        private set => Set(ref _loaded, value);
    }

    public string SearchText
    {
        get => _searchText;
        set => Set(ref _searchText, value);
    }

    public MapColorMode ColorMode
    {
        get => _colorMode;
        set
        {
            if (Set(ref _colorMode, value))
            {
                ColorModeChanged?.Invoke(this, value);
            }
        }
    }

    public ResourceKind ResourceKind
    {
        get => _resourceKind;
        set
        {
            if (Set(ref _resourceKind, value))
            {
                ResourceKindChanged?.Invoke(this, value);
            }
        }
    }

    public bool ShowCharacters
    {
        get => _showCharacters;
        set
        {
            if (Set(ref _showCharacters, value))
            {
                ShowCharactersChanged?.Invoke(this, value);
            }
        }
    }

    public bool ShowBridges
    {
        get => _showBridges;
        set
        {
            if (Set(ref _showBridges, value))
            {
                JumpBridgeSettingService.SetShowBridge(value);
                BridgesChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IntelRunning
    {
        get => _intelRunning;
        private set => Set(ref _intelRunning, value);
    }

    /// <summary>情报是否接入 ZKB 击杀流。</summary>
    public bool ZkbIntel
    {
        get => _zkbIntel;
        set
        {
            if (Set(ref _zkbIntel, value))
            {
                ApplyZkbSubscription();
            }
        }
    }

    /// <summary>ZKB 击杀在场上的保留时长（分钟，落 MapIntelConfig.ZKBDuration）。</summary>
    public double ZkbDurationMinutes
    {
        get => _zkbDurationMinutes;
        set
        {
            if (Set(ref _zkbDurationMinutes, Math.Clamp(value, 1, 180)))
            {
                SaveIntelKeywords();
            }
        }
    }

    /// <summary>是否有运行中的预警会话可接入情报（ZKB 击杀不依赖会话）。</summary>
    public bool IntelAvailable => ChannelIntelManager.Current.GetActiveListeners().Count > 0;

    /// <summary>情报面板的提示：有频道会话时省略，没有时说明"仅 ZKB 也能用"。</summary>
    public string IntelHintText => IntelAvailable ? FindString("MapPage_IntelHint") : FindString("MapPage_IntelNoSessions");

    public MapSystemNode? SelectedSystem
    {
        get => _selectedSystem;
        private set
        {
            if (Set(ref _selectedSystem, value))
            {
                OnPropertyChanged(nameof(SelectedSecurityText));
                OnPropertyChanged(nameof(SelectedInfoText));
                UpdateSelectedStats();
            }
        }
    }

    private void UpdateSelectedStats()
    {
        _selectedKills = SelectedSystem is not null && _killValues.TryGetValue(SelectedSystem.Id, out var k) ? k : 0;
        _selectedJumps = SelectedSystem is not null && _jumpValues.TryGetValue(SelectedSystem.Id, out var j) ? j : 0;
        OnPropertyChanged(nameof(SelectedKillsText));
        OnPropertyChanged(nameof(SelectedJumpsText));
        OnPropertyChanged(nameof(SelectedSovText));
    }

    public string SelectedSecurityText => SelectedSystem is null
        ? string.Empty
        : (SelectedSystem.Security <= 0 ? "0.0" : SelectedSystem.Security.ToString("0.0"));

    public string SelectedInfoText => SelectedSystem is null
        ? string.Empty
        : $"{SelectedSystem.RegionName} · {SelectedSystem.Name}";

    public string SelectedKillsText => _selectedKills.ToString("N0");
    public string SelectedJumpsText => _selectedJumps.ToString("N0");

    /// <summary>选中星系的主权联盟名（无主权为空）。</summary>
    public string SelectedSovText => SelectedSystem is null
        ? string.Empty
        : SovService.Current.FirstOrDefault(p => p.SystemIds.Contains(SelectedSystem.Id))?.AllianceName ?? string.Empty;

    public double MaxLy
    {
        get => _maxLy;
        set => Set(ref _maxLy, Math.Clamp(value, 1, 20));
    }

    public bool CapitalMode
    {
        get => _capitalMode;
        set => Set(ref _capitalMode, value);
    }

    public bool UseGates
    {
        get => _useGates;
        set => Set(ref _useGates, value);
    }

    /// <summary>省钱优先（旗舰跳按距离加权，仅旗舰模式生效）。</summary>
    public bool PreferCheaper
    {
        get => _preferCheaper;
        set => Set(ref _preferCheaper, value);
    }

    /// <summary>选中的旗舰（用于算最大跳跃距离与每光年燃料）。</summary>
    public CapitalJumpShipInfo? JumpShip
    {
        get => _jumpShip;
        set
        {
            if (Set(ref _jumpShip, value))
            {
                OnPropertyChanged(nameof(MaxLy));
                OnPropertyChanged(nameof(PerLyFuel));
                OnPropertyChanged(nameof(ShowJumpFreighters));
                AutoFillMaxLyFromShip();
            }
        }
    }

    public int JumpDriveCalibration
    {
        get => _jdc;
        set
        {
            if (Set(ref _jdc, Math.Clamp(value, 0, 5)))
            {
                OnPropertyChanged(nameof(PerLyFuel));
                AutoFillMaxLyFromShip();
            }
        }
    }

    public int JumpFuelConservation
    {
        get => _jfc;
        set
        {
            if (Set(ref _jfc, Math.Clamp(value, 0, 5)))
            {
                OnPropertyChanged(nameof(PerLyFuel));
            }
        }
    }

    /// <summary>战略货舰（GroupID 1089）才吃"Jump Freighters"技能。</summary>
    public int JumpFreighters
    {
        get => _jumpFreighters;
        set
        {
            if (Set(ref _jumpFreighters, Math.Clamp(value, 0, 5)))
            {
                OnPropertyChanged(nameof(PerLyFuel));
            }
        }
    }

    /// <summary>是否显示"Jump Freighters"技能输入（仅战略货舰）。</summary>
    public bool ShowJumpFreighters => JumpShip?.GroupID == StrategicFreighterGroupId;

    private const int StrategicFreighterGroupId = 1089;

    /// <summary>当前旗舰参数下的最大跳跃距离（光年）。</summary>
    public double ShipMaxJump => JumpShip is null ? 0 : JumpShip.MaxLY + JumpShip.MaxLY * 0.2 * JumpDriveCalibration;

    /// <summary>当前旗舰参数下的每光年燃料消耗。</summary>
    public double PerLyFuel
    {
        get
        {
            if (JumpShip is null)
            {
                return 0;
            }

            var baseFuel = JumpShip.PerLYFuel;
            if (JumpShip.GroupID == StrategicFreighterGroupId)
            {
                baseFuel -= baseFuel * 0.1 * JumpFreighters;
            }

            return baseFuel - baseFuel * 0.1 * JumpFuelConservation;
        }
    }

    public AuthorizedCharacterData? AutopilotCharacter
    {
        get => _autopilotCharacter;
        set => Set(ref _autopilotCharacter, value);
    }

    /// <summary>情报排除关键词（空格/逗号/分号分隔）。</summary>
    public string IntelExcludeKeywords
    {
        get => _intelExcludeKeywords;
        set => Set(ref _intelExcludeKeywords, value);
    }

    /// <summary>情报包含关键词（非空时仅包含任一关键词的情报展示）。</summary>
    public string IntelIncludeKeywords
    {
        get => _intelIncludeKeywords;
        set => Set(ref _intelIncludeKeywords, value);
    }

    /// <summary>导航概览（总跳数 / 旗舰跳 / 燃料）。</summary>
    public string NavSummary
    {
        get => _navSummary;
        private set => Set(ref _navSummary, value);
    }

    public event EventHandler<MapColorMode>? ColorModeChanged;
    public event EventHandler<ResourceKind>? ResourceKindChanged;
    public event EventHandler<bool>? ShowCharactersChanged;
    public event EventHandler? BridgesChanged;
    public event EventHandler? StatisticsLoaded;
    public event EventHandler<IReadOnlyList<IntelMarker>>? IntelMarkersChanged;
    public event EventHandler? NavigationCompleted;
    public event EventHandler<int[]>? CoverChanged;
    public event EventHandler<(int ShipTypeId, SKBitmap Bitmap)>? IntelShipImageLoaded;
    public event EventHandler<(long CharacterId, SKBitmap Bitmap)>? PortraitLoaded;

    public event PropertyChangedEventHandler? PropertyChanged;

    // ---------- 数据装载 ----------

    public async Task LoadAsync(Func<List<MapSystemNode>, List<(int From, int To)>, Task> setData)
    {
        if (IsLoading || IsLoaded)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var systems = await Task.Run(() => Core.Services.DB.MapSolarSystemService.QueryAll().Where(p => !p.IsSpecial()).ToList());
            var links = await Task.Run(BuildLinks);
            var nodes = await Task.Run(() => BuildNodes(systems));
            _systems = systems;
            _nodeById = nodes.ToDictionary(p => p.Id);
            BuildAdjacency(links);
            await setData(nodes, links);

            var regions = await Task.Run(() => Core.Services.DB.MapRegionService.QueryAll()
                .Where(p => !p.IsSpecial())
                .OrderBy(p => p.RegionName)
                .ToList());
            Regions.Clear();
            foreach (var region in regions)
            {
                Regions.Add(region);
            }

            // 筛选下拉（首个"全部星域"哨兵）
            FilterRegions.Clear();
            FilterRegions.Add(new MapRegion { RegionID = 0, RegionName = FindString("MapPage_Filter_AllRegions") });
            foreach (var region in regions)
            {
                FilterRegions.Add(region);
            }

            _filterRegion ??= FilterRegions.FirstOrDefault();

            // 已授权角色（导航"在游戏中设置航点"用）
            var characters = await _dispatcher.InvokeAsync(() => CharacterStore.Characters.ToList());
            AutopilotCharacters.Clear();
            foreach (var character in characters)
            {
                AutopilotCharacters.Add(character);
            }

            AutopilotCharacter = AutopilotCharacters.FirstOrDefault();

            // 旗舰船表（默认选第一个，导航与一跳覆盖共用）
            JumpShips.Clear();
            foreach (var ship in Core.EVEHelpers.CapitalJumpShipInfoHelper.GetInfos() ?? [])
            {
                JumpShips.Add(ship);
            }

            JumpShip = JumpShips.FirstOrDefault();

            // 跳桥：读配置并保证已分配分组号的落地
            ReloadBridges();

            RefreshIntelAvailability();
            IsLoaded = true;

            _ = LoadStatisticsAsync();
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

    private static List<MapSystemNode> BuildNodes(IEnumerable<MapSolarSystem> systems)
    {
        // 与 SolarSystemPosHelper 一致：过滤特殊星系、Y 轴翻转（游戏原点左下 → 屏幕左上）
        var list = systems.ToList();
        var regions = Core.Services.DB.MapRegionService.QueryAll().ToDictionary(p => p.RegionID, p => p.RegionName);
        var maxY = list.Max(p => p.Y2);
        var nodes = new List<MapSystemNode>(list.Count);
        foreach (var system in list)
        {
            regions.TryGetValue(system.RegionID, out var regionName);
            nodes.Add(new MapSystemNode
            {
                Id = system.SolarSystemID,
                Name = system.SolarSystemName ?? string.Empty,
                Security = system.Security,
                RegionId = system.RegionID,
                RegionName = regionName ?? string.Empty,
                NX = system.X2,
                NY = maxY - system.Y2,
            });
        }
        return nodes;
    }

    private static List<(int From, int To)> BuildLinks()
    {
        var jumps = Core.Services.DB.MapSolarSystemJumpService.QueryAll();
        var result = new List<(int, int)>(jumps.Count);
        foreach (var jump in jumps)
        {
            result.Add((jump.FromSolarSystemID, jump.ToSolarSystemID));
        }
        return result;
    }

    /// <summary>页面把画布选中节点回填（含邻接星系，供信息卡显示）。</summary>
    public void SetSelectedSystem(MapSystemNode? node, IReadOnlyList<MapSystemNode> neighbors)
    {
        SelectedSystem = node;
        Neighbors.Clear();
        foreach (var neighbor in neighbors)
        {
            Neighbors.Add(neighbor);
        }
    }

    /// <summary>ESI 全星系击杀/通行量统计（失败静默，仅日志）。</summary>
    private async Task LoadStatisticsAsync()
    {
        try
        {
            var result = await Task.Run(async () =>
            {
                var esi = Core.Services.ESIService.GetDefaultESI();
                var kills = new Dictionary<int, double>();
                var jumps = new Dictionary<int, double>();
                var stats = new Dictionary<int, SystemStatInfo>();
                var killResp = await esi.Universe.GetSystemKillsAsync();
                if (killResp?.Model is not null)
                {
                    foreach (var kill in killResp.Model)
                    {
                        var id = (int)kill.SystemId;
                        kills[id] = kill.ShipKills;
                        stats[id] = new SystemStatInfo
                        {
                            ShipKills = kill.ShipKills,
                            NpcKills = kill.NpcKills,
                            PodKills = kill.PodKills,
                        };
                    }
                }
                else
                {
                    Core.Log.Error("星图：GetSystemKillsAsync 失败");
                }

                var jumpResp = await esi.Universe.GetSystemJumpsAsync();
                if (jumpResp?.Model is not null)
                {
                    foreach (var jump in jumpResp.Model)
                    {
                        var id = (int)jump.SystemId;
                        jumps[id] = jump.ShipJumps;
                        if (stats.TryGetValue(id, out var stat))
                        {
                            stats[id] = new SystemStatInfo
                            {
                                ShipKills = stat.ShipKills,
                                NpcKills = stat.NpcKills,
                                PodKills = stat.PodKills,
                                Jumps = jump.ShipJumps,
                            };
                        }
                        else
                        {
                            stats[id] = new SystemStatInfo { Jumps = jump.ShipJumps };
                        }
                    }
                }
                else
                {
                    Core.Log.Error("星图：GetSystemJumpsAsync 失败");
                }
                return (kills, jumps, stats);
            });

            _killValues = result.kills;
            _jumpValues = result.jumps;
            _systemStats = result.stats;
            _killMax = result.kills.Count > 0 ? result.kills.Values.Max() : 0;
            _jumpMax = result.jumps.Count > 0 ? result.jumps.Values.Max() : 0;
            _ = _dispatcher.BeginInvoke(() =>
            {
                UpdateSelectedStats();
                StatisticsLoaded?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    /// <summary>取星系 ESI 统计（详情页用）。</summary>
    public SystemStatInfo? GetSystemStat(int systemId)
        => _systemStats.TryGetValue(systemId, out var stat) ? stat : null;

    /// <summary>为指定着色模式填充节点 Heat 值。</summary>
    public void FillHeat(IReadOnlyList<MapSystemNode> nodes, MapColorMode mode)
    {
        var values = mode == MapColorMode.Kills ? _killValues : _jumpValues;
        foreach (var node in nodes)
        {
            node.Heat = values.TryGetValue(node.Id, out var v) ? v : -1;
        }
    }

    public double KillsMax => _killMax;
    public double JumpsMax => _jumpMax;
    public double ResourceMax => _resourceMax;

    /// <summary>把 ESI 统计写入选中星系信息。</summary>
    public void RefreshSelectedInfo() => UpdateSelectedStats();

    // ---------- 着色：主权 ----------

    private bool _sovLoaded;

    /// <summary>装载（或复用）主权数据并按分组号写入节点。</summary>
    public async Task ApplySovAsync(bool forceRefresh = false)
    {
        var infos = await SovService.LoadAsync(forceRefresh);
        var map = new Dictionary<int, long>();
        foreach (var info in infos)
        {
            foreach (var systemId in info.SystemIds)
            {
                if(map.ContainsKey(systemId))
                    map[systemId] = info.GroupId;
            }
        }

        foreach (var node in _nodeById.Values)
        {
            node.GroupId = map.TryGetValue(node.Id, out var groupId) ? groupId : 0;
        }

        _sovLoaded = true;
        OnPropertyChanged(nameof(SelectedSovText));
        // 注意：这里绝不能再发 ColorModeChanged——页面处理器收到后会再次调 ApplySovAsync，
        // 而本方法在缓存命中时全程同步执行 → 同步无限递归 → StackOverflowException（阶段 64 实机）。
        // 重着色由页面在事件处理器末尾统一调 MapCanvas.SetColorMode 完成。
    }

    public bool IsSovLoaded => _sovLoaded;

    // ---------- 着色：行星资源 ----------

    /// <summary>装载行星资源（幂等，首次较慢）并写入节点资源值。</summary>
    public async Task ApplyResourceAsync(ResourceKind kind)
    {
        if (!MapResourceService.IsLoaded)
        {
            IsLoading = true;
            try
            {
                await MapResourceService.LoadAsync(_systems);
            }
            finally
            {
                IsLoading = false;
            }
        }

        _resourceMax = 0;
        foreach (var node in _nodeById.Values)
        {
            var value = MapResourceService.SystemResources.TryGetValue(node.Id, out var resources)
                ? MapResourceService.GetValue(resources, kind)
                : 0;
            node.Resource = value;
            if (value > _resourceMax)
            {
                _resourceMax = value;
            }
        }

        // 同 ApplySovAsync：这里不能再发 ColorModeChanged（页面会再次调本方法 → 同步无限递归）。
        // 重着色由页面在事件处理器末尾统一调 MapCanvas.SetColorMode 完成。
    }

    // ---------- 跳桥 ----------

    private void ReloadBridges()
    {
        Bridges.Clear();
        foreach (var bridge in JumpBridgeSettingService.GetValue())
        {
            Bridges.Add(bridge);
        }

        _showBridges = JumpBridgeSettingService.IsShowBridge();
        OnPropertyChanged(nameof(ShowBridges));
        BridgesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>跳桥增删后刷新（设置窗回调）。</summary>
    public void RefreshBridges() => ReloadBridges();

    /// <summary>画布用的去重跳桥对（每对只画一次）。</summary>
    public List<(int A, int B)> GetBridgePairs()
    {
        var seen = new HashSet<long>();
        var result = new List<(int, int)>();
        foreach (var bridge in JumpBridgeSettingService.GetValue())
        {
            if (bridge.System1 <= 0 || bridge.System2 <= 0)
            {
                continue;
            }

            var lo = Math.Min(bridge.System1, bridge.System2);
            var hi = Math.Max(bridge.System1, bridge.System2);
            if (seen.Add(lo * 1_000_000L + hi))
            {
                result.Add((lo, hi));
            }
        }
        return result;
    }

    // ---------- 星域 / 安等筛选 ----------

    private MapRegion? _filterRegion;
    private double _filterSecMin = -1;
    private double _filterSecMax = 1.0;

    /// <summary>筛选下拉的候选（首个是"全部星域"哨兵，RegionID = 0）。</summary>
    public ObservableCollection<MapRegion> FilterRegions { get; } = [];

    /// <summary>筛选星域（哨兵 / null = 全部星域）。</summary>
    public MapRegion? FilterRegion
    {
        get => _filterRegion;
        set => Set(ref _filterRegion, value);
    }

    public double FilterSecMin
    {
        get => _filterSecMin;
        set => Set(ref _filterSecMin, Math.Clamp(value, -1, 1));
    }

    public double FilterSecMax
    {
        get => _filterSecMax;
        set => Set(ref _filterSecMax, Math.Clamp(value, -1, 1));
    }

    /// <summary>筛选是否已生效（页面可用于提示）。</summary>
    public bool HasSystemFilter => _filterRegion is { RegionID: > 0 } || _filterSecMin > -1 || _filterSecMax < 1.0;

    /// <summary>筛选状态变化（页面据此让画布重建底图）。</summary>
    public event EventHandler? NodeStatesChanged;

    /// <summary>
    /// 应用筛选：命中的星系保持原色，未命中的灰化（对应 WinUI 的批量 Enable 灰化）。
    /// 只影响显示，不影响命中测试与导航。
    /// </summary>
    public void ApplySystemFilter()
    {
        var regionId = _filterRegion?.RegionID ?? 0;
        var min = Math.Min(_filterSecMin, _filterSecMax);
        var max = Math.Max(_filterSecMin, _filterSecMax);
        foreach (var node in _nodeById.Values)
        {
            var regionOk = regionId <= 0 || node.RegionId == regionId;
            var secOk = node.Security >= min - 0.0001 && node.Security <= max + 0.0001;
            node.Enabled = regionOk && secOk;
        }

        OnPropertyChanged(nameof(HasSystemFilter));
        NodeStatesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>清除筛选（全部恢复原色）。</summary>
    public void ClearSystemFilter()
    {
        FilterRegion = FilterRegions.FirstOrDefault();
        FilterSecMin = -1;
        FilterSecMax = 1.0;
        foreach (var node in _nodeById.Values)
        {
            node.Enabled = true;
        }

        OnPropertyChanged(nameof(HasSystemFilter));
        NodeStatesChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---------- 搜索 ----------

    public void Search(string? text, int maxCount = 12)
    {
        SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(text) || text.Length < 2)
        {
            return;
        }

        var nodes = AllNodes;
        if (nodes is null)
        {
            return;
        }

        var count = 0;
        foreach (var node in nodes)
        {
            if (node.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                SearchResults.Add(node);
                if (++count >= maxCount)
                {
                    break;
                }
            }
        }
    }

    /// <summary>画布装载后由页面注入全部节点（搜索用）。</summary>
    public IReadOnlyList<MapSystemNode>? AllNodes { get; set; }

    public void ClearSearch()
    {
        SearchResults.Clear();
        SearchText = string.Empty;
    }

    /// <summary>按 Id 取节点（导航结果 / 详情页用，不依赖画布控件）。</summary>
    public bool TryGetNode(int id, out MapSystemNode? node) => _nodeById.TryGetValue(id, out node);

    private Dictionary<int, List<int>> _adjacency = [];

    private void BuildAdjacency(List<(int From, int To)> links)
    {
        _adjacency = [];
        foreach (var (from, to) in links)
        {
            if (!_nodeById.ContainsKey(from) || !_nodeById.ContainsKey(to) || from == to)
            {
                continue;
            }

            if (!_adjacency.TryGetValue(from, out var list))
            {
                _adjacency[from] = list = [];
            }

            if (!list.Contains(to))
            {
                list.Add(to);
            }
        }
    }

    /// <summary>星门邻接星系 Id（详情页"邻接"页签用）。</summary>
    public IReadOnlyList<int> GetNeighbors(int id) => _adjacency.TryGetValue(id, out var list) ? list : [];

    // ---------- 情报模式 ----------

    public void RefreshIntelAvailability()
    {
        OnPropertyChanged(nameof(IntelAvailable));
        OnPropertyChanged(nameof(IntelHintText));
        ActiveListeners.Clear();
        foreach (var listener in ChannelIntelManager.Current.GetActiveListeners())
        {
            ActiveListeners.Add(listener);
        }
    }

    public void StartIntel()
    {
        if (IntelRunning)
        {
            return;
        }

        // 载入/合并情报配置（与 WinUI 共用 MapSettings.json）
        var config = MapSettingService.GetIntel();
        IntelExcludeKeywords = config.Exclusions is { Count: > 0 } ? string.Join(' ', config.Exclusions.Select(p => p.Name)) : IntelExcludeKeywords;
        IntelIncludeKeywords = config.Inclusions is { Count: > 0 } ? string.Join(' ', config.Inclusions.Select(p => p.Name)) : IntelIncludeKeywords;
        _zkbIntel = config.ZKB;
        _zkbDurationMinutes = config.ZKBDuration > 0 ? Math.Round(config.ZKBDuration / 60.0, 0) : 20;
        OnPropertyChanged(nameof(ZkbIntel));
        OnPropertyChanged(nameof(ZkbDurationMinutes));

        ChannelIntelManager.Current.OnIgnoreJumpsIntelUpdate += Intel_OnIgnoreJumpsIntelUpdate;
        ChannelIntelManager.Current.ListenChannelIntel(ChannelIntelManager.Current.GetActiveListeners());
        IntelRunning = true;
        ApplyZkbSubscription();

        _intelExpiryTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(10),
        };
        _intelExpiryTimer.Tick += IntelExpiry_Tick;
        _intelExpiryTimer.Start();
    }

    public void StopIntel()
    {
        if (!IntelRunning)
        {
            return;
        }

        ChannelIntelManager.Current.OnIgnoreJumpsIntelUpdate -= Intel_OnIgnoreJumpsIntelUpdate;
        ChannelIntelManager.Current.UnListenChannelIntel();
        ApplyZkbSubscription();
        IntelRunning = false;
        _intelExpiryTimer?.Stop();
        _intelExpiryTimer = null;
        IntelMessages.Clear();
        _intelSystems.Clear();
        IntelMarkersChanged?.Invoke(this, []);
    }

    /// <summary>情报红圈数据变化（页面据此刷新画布覆盖层）。</summary>
    private void ApplyZkbSubscription()
    {
        var hub = Services.KB.ZkbKillStreamHub.Current;
        hub.Matched -= OnZkbMatched;
        if (IntelRunning && _zkbIntel)
        {
            hub.Matched += OnZkbMatched;
            _ = StartZkbAsync();
        }
        else
        {
            StopZkb();
        }
    }

    private bool _zkbStarted;

    private async Task StartZkbAsync()
    {
        try
        {
            if (!_zkbStarted)
            {
                _zkbStarted = await Services.KB.ZkbKillStreamHub.Current.StartAsync();
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private void StopZkb()
    {
        if (_zkbStarted)
        {
            Services.KB.ZkbKillStreamHub.Current.Stop();
            _zkbStarted = false;
        }
    }

    /// <summary>一个星系的情报聚合（舰船统计 / 击杀去重 / 时间范围）。</summary>
    private sealed class IntelSystem
    {
        public int SystemId { get; init; }
        public Dictionary<int, int> Ships { get; } = [];
        public HashSet<long> ZkbKillmails { get; } = [];
        public DateTime OldestUtc { get; set; } = DateTime.MaxValue;
    }

    private readonly Dictionary<int, IntelSystem> _intelSystems = [];
    private readonly HashSet<int> _intelShipRequested = [];

    private void OnZkbMatched(Core.Models.KB.KBItemInfo info)
    {
        if (info is null)
        {
            return;
        }

        var detail = info.SKBDetail;
        if (detail is null || detail.SolarSystemId <= 0)
        {
            return;
        }

        // 攻方舰船（与 WinUI 一致：同一 attackerId 只计一次）
        var attackerIds = new HashSet<int>();
        var ships = new List<int>();
        if (detail.Attackers is { Count: > 0 })
        {
            foreach (var attacker in detail.Attackers)
            {
                if (attacker.CharacterId is > 0 && !attackerIds.Add((int)attacker.CharacterId))
                {
                    continue;
                }

                if (attacker.ShipTypeId > 0)
                {
                    ships.Add(attacker.ShipTypeId);
                }
            }
        }

        var text = BuildZkbText(info);
        _dispatcher.BeginInvoke(() =>
        {
            if (!IntelRunning || !_zkbIntel)
            {
                return;
            }

            if (!PassKeywordFilter(text))
            {
                return;
            }

            var systemId = (int)detail.SolarSystemId;
            var entry = GetIntelSystem(systemId);
            if (entry.ZkbKillmails.Add(detail.KillmailId))
            {
                foreach (var shipTypeId in ships)
                {
                    entry.Ships[shipTypeId] = entry.Ships.GetValueOrDefault(shipTypeId) + 1;
                    RequestIntelShipImage(shipTypeId);
                }
            }

            entry.OldestUtc = entry.OldestUtc > detail.KillmailTime ? detail.KillmailTime : entry.OldestUtc;

            AddIntelMessage(new IntelMsgItem
            {
                TimeUtc = detail.KillmailTime,
                TimeText = detail.KillmailTime.ToLocalTime().ToString("HH:mm:ss"),
                Content = text,
                SystemId = systemId,
                SystemName = TryGetNode(systemId, out var node) && node is not null ? node.Name : systemId.ToString(),
                Listener = "ZKB",
                IsZkb = true,
            });

            IntelMarkersChanged?.Invoke(this, BuildIntelMarkers());
        });
    }

    private static string BuildZkbText(Core.Models.KB.KBItemInfo info)
    {
        var ship = info.Type?.TypeName;
        var victim = info.Victim?.Name;
        var finalBlow = info.FinalBlow?.Name;
        var text = string.Join(" · ", new[] { victim, ship }.Where(p => !string.IsNullOrWhiteSpace(p)));
        if (string.IsNullOrWhiteSpace(text))
        {
            text = info.SKBDetail?.KillmailId.ToString() ?? "Kill";
        }

        return string.IsNullOrWhiteSpace(finalBlow) ? text : $"{text} ← {finalBlow}";
    }

    private IntelSystem GetIntelSystem(int systemId)
    {
        if (!_intelSystems.TryGetValue(systemId, out var entry))
        {
            entry = new IntelSystem { SystemId = systemId };
            _intelSystems[systemId] = entry;
        }

        return entry;
    }

    /// <summary>舰船图标按需下载（每个类型一次）。</summary>
    private void RequestIntelShipImage(int shipTypeId)
    {
        if (!_intelShipRequested.Add(shipTypeId))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var url = GameImageHelper.BuildTypeImageUrl(shipTypeId, 64);
                if (url is null)
                {
                    return;
                }

                var bytes = await Core.Helpers.HttpHelper.GetByteArrayAsync(url);
                if (bytes is not { Length: > 0 })
                {
                    return;
                }

                var bitmap = SKBitmap.Decode(bytes);
                if (bitmap is not null)
                {
                    await _dispatcher.BeginInvoke(() => IntelShipImageLoaded?.Invoke(this, (shipTypeId, bitmap)));
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        });
    }

    private void Intel_OnIgnoreJumpsIntelUpdate(object sender, IEnumerable<Core.Models.EarlyWarningContent> contents)
    {
        var items = contents as IReadOnlyList<Core.Models.EarlyWarningContent> ?? contents.ToList();
        _ = _dispatcher.BeginInvoke(() =>
        {
            foreach (var content in items)
            {
                if (content.IntelType == IntelChatType.Ignore)
                {
                    continue;
                }

                if (!PassKeywordFilter(content.Content))
                {
                    continue;
                }

                var item = new IntelMsgItem
                {
                    TimeUtc = content.Time,
                    TimeText = content.Time.ToLocalTime().ToString("HH:mm:ss"),
                    Content = content.Content,
                    SystemId = content.SolarSystemId,
                    SystemName = content.SolarSystemName ?? string.Empty,
                    Listener = content.Listener ?? string.Empty,
                    IsClear = content.IntelType == IntelChatType.Clear,
                };

                if (content.IntelType == IntelChatType.Clear)
                {
                    // "清怪"：清空该星系已有情报（只留本条）
                    for (var i = IntelMessages.Count - 1; i >= 0; i--)
                    {
                        if (IntelMessages[i].SystemId == content.SolarSystemId && !IntelMessages[i].IsClear)
                        {
                            IntelMessages.RemoveAt(i);
                        }
                    }

                    if (_intelSystems.TryGetValue(content.SolarSystemId, out var entry))
                    {
                        entry.Ships.Clear();
                        entry.ZkbKillmails.Clear();
                    }
                }
                else if (content.SolarSystemId > 0)
                {
                    var entry = GetIntelSystem(content.SolarSystemId);
                    entry.OldestUtc = entry.OldestUtc > content.Time ? content.Time : entry.OldestUtc;
                }

                AddIntelMessage(item);
            }

            IntelMarkersChanged?.Invoke(this, BuildIntelMarkers());
        });
    }

    private void AddIntelMessage(IntelMsgItem item)
    {
        IntelMessages.Insert(0, item);
        var max = MaxIntelMsgs;
        var configMax = (int)MapSettingService.Value.Intel.MaxMsgCount;
        if (configMax > 0)
        {
            max = configMax;
        }

        while (IntelMessages.Count > max)
        {
            IntelMessages.RemoveAt(IntelMessages.Count - 1);
        }
    }

    private bool PassKeywordFilter(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return true;
        }

        var include = SplitKeywords(IntelIncludeKeywords);
        if (include.Count > 0 && !include.Any(content.Contains))
        {
            return false;
        }

        var exclude = SplitKeywords(IntelExcludeKeywords);
        return !exclude.Any(content.Contains);
    }

    private void IntelExpiry_Tick(object? sender, EventArgs e)
    {
        var config = MapSettingService.Value.Intel;
        var channelSeconds = config.ChannelDuration > 0 ? config.ChannelDuration : 1200;
        var zkbSeconds = config.ZKBDuration > 0 ? config.ZKBDuration : 1200;
        var now = DateTime.UtcNow;
        var channelDeadline = now.AddSeconds(-channelSeconds);
        var zkbDeadline = now.AddSeconds(-zkbSeconds);
        var removed = false;

        for (var i = IntelMessages.Count - 1; i >= 0; i--)
        {
            var item = IntelMessages[i];
            var deadline = item.IsZkb ? zkbDeadline : channelDeadline;
            if (item.TimeUtc < deadline)
            {
                IntelMessages.RemoveAt(i);
                removed = true;
            }
        }

        // 舰船聚合按 ZKB 时长过期（按星系粒度重建，简单可靠）
        foreach (var entry in _intelSystems.Values.ToList())
        {
            if (entry.OldestUtc != DateTime.MaxValue && entry.OldestUtc < zkbDeadline)
            {
                entry.Ships.Clear();
                entry.ZkbKillmails.Clear();
                removed = true;
            }
        }

        if (removed)
        {
            IntelMarkersChanged?.Invoke(this, BuildIntelMarkers());
        }
    }

    private IReadOnlyList<IntelMarker> BuildIntelMarkers()
    {
        var markers = new List<IntelMarker>();
        foreach (var entry in _intelSystems.Values)
        {
            var shipCount = entry.Ships.Values.Sum();
            var channelCount = IntelMessages.Count(p => p.SystemId == entry.SystemId && !p.IsZkb);
            if (shipCount <= 0 && channelCount <= 0)
            {
                continue;
            }

            markers.Add(new IntelMarker
            {
                SystemId = entry.SystemId,
                Weight = shipCount + channelCount * 5,
                Ships = entry.Ships.OrderByDescending(p => p.Value).ToDictionary(p => p.Key, p => p.Value),
                ShipCount = shipCount,
                ZkbCount = entry.ZkbKillmails.Count,
                ChannelCount = channelCount,
                OldestUtc = entry.OldestUtc == DateTime.MaxValue ? default : entry.OldestUtc,
            });
        }

        return markers;
    }

    private static List<string> SplitKeywords(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split([' ', ',', ';', '，', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public void SaveIntelKeywords()
    {
        var config = MapSettingService.GetIntel();
        config.Exclusions = new System.Collections.ObjectModel.ObservableCollection<IdName>(
            SplitKeywords(IntelExcludeKeywords).Select(p => new IdName { Name = p }));
        config.Inclusions = new System.Collections.ObjectModel.ObservableCollection<IdName>(
            SplitKeywords(IntelIncludeKeywords).Select(p => new IdName { Name = p }));
        config.ZKB = _zkbIntel;
        config.ZKBDuration = (float)(_zkbDurationMinutes * 60);
        MapSettingService.SaveIntel(config);
    }

    // ---------- 导航 ----------

    public void AddWaypoint(MapSystemNode? node)
    {
        if (node is not null && Waypoints.All(p => p.Id != node.Id))
        {
            Waypoints.Add(node);
        }
    }

    public void RemoveWaypoint(MapSystemNode node) => Waypoints.Remove(node);

    public void AddAvoid(MapSystemNode? node)
    {
        if (node is not null && AvoidSystems.All(p => p.Id != node.Id))
        {
            AvoidSystems.Add(node);
        }
    }

    public void RemoveAvoid(MapSystemNode node) => AvoidSystems.Remove(node);

    public string? LastNavigationError { get; private set; }

    public async Task<bool> NavigateAsync()
    {
        if (Waypoints.Count < 2)
        {
            LastNavigationError = FindString("MapPage_Error_NeedWaypoints");
            return false;
        }

        var waypoints = Waypoints.Select(p => p.Id).ToList();
        var avoid = AvoidSystems.Select(p => p.Id).ToList();
        var capital = CapitalMode;
        var useGates = UseGates;
        var maxLy = CapitalMode && JumpShip is not null ? ShipMaxJump : MaxLy;
        var mode = PreferCheaper ? 1 : 0;
        var bridge = capital ? null : JumpBridgeSettingService.GetBridgesDict();
        var bridgePairs = GetBridgePairs();

        var path = await Task.Run(() =>
        {
            try
            {
                var result = new List<int>();
                for (var i = 0; i < waypoints.Count - 1; i++)
                {
                    List<int> segment;
                    if (capital)
                    {
                        segment = Core.EVEHelpers.ShortestPathHelper.CalCapitalJumpPath(
                            waypoints[i], waypoints[i + 1], maxLy, useGates, avoid, mode);
                    }
                    else
                    {
                        segment = Core.EVEHelpers.ShortestPathHelper.CalStargatePath(
                            waypoints[i], waypoints[i + 1], avoid, bridge);
                    }

                    if (segment is not { Count: > 0 })
                    {
                        return null;
                    }

                    // Dijkstras 返回 终点→起点，反转为 起点→终点；段间去重衔接点
                    segment.Reverse();
                    if (result.Count > 0)
                    {
                        segment.RemoveAt(0);
                    }

                    result.AddRange(segment);
                }
                return result;
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                return null;
            }
        });

        if (path is not { Count: > 0 })
        {
            LastNavigationError = FindString("MapPage_Error_NoPath");
            return false;
        }

        BuildNavResult(path, capital, useGates, bridgePairs);
        NavigationCompleted?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>航线（星系 Id 列表）与关键航点下标。</summary>
    public IReadOnlyList<int> LastPath { get; private set; } = [];
    public IReadOnlyList<int> LastWaypointIndices { get; private set; } = [];

    private void BuildNavResult(List<int> path, bool capital, bool useGates, List<(int A, int B)> bridges)
    {
        var positionDic = Core.EVEHelpers.SolarSystemPosHelper.PositionDic;
        var waypointIds = Waypoints.Select(p => p.Id).ToHashSet();
        var bridgeSet = bridges.SelectMany(p => new[] { (p.A, p.B), (p.B, p.A) }).ToHashSet();
        var perLyFuel = PerLyFuel;
        var result = new List<NavResultItem>(path.Count);
        var waypointIndices = new List<int>();
        double ly = 0;
        for (var i = 0; i < path.Count; i++)
        {
            var id = path[i];
            if (!_nodeById.TryGetValue(id, out var node))
            {
                continue;
            }

            var navType = 0;
            var fuel = 0d;
            if (i > 0)
            {
                var prevId = path[i - 1];
                var prev = positionDic.TryGetValue(prevId, out var p1) ? p1 : null;
                var curr = positionDic.TryGetValue(id, out var p2) ? p2 : null;
                if (prev is not null && curr is not null)
                {
                    ly = Math.Sqrt(Math.Pow(prev.X - curr.X, 2) + Math.Pow(prev.Y - curr.Y, 2) + Math.Pow(prev.Z - curr.Z, 2)) / 9460730472580800;
                }

                if (capital && !useGates)
                {
                    navType = 2;
                }
                else if (bridgeSet.Contains((prevId, id)))
                {
                    navType = 3;
                }
                else if (prev?.JumpTo is { Count: > 0 } && prev.JumpTo.Contains(id))
                {
                    navType = 1;
                }
                else
                {
                    navType = 2;
                }

                if (navType == 2)
                {
                    fuel = perLyFuel * ly;
                }
            }

            if (waypointIds.Contains(id))
            {
                waypointIndices.Add(result.Count);
            }

            result.Add(new NavResultItem
            {
                Index = result.Count + 1,
                Node = node,
                SecurityText = node.Security <= 0 ? "0.0" : node.Security.ToString("0.0"),
                RegionName = node.RegionName,
                DistanceLy = ly,
                NavType = navType,
                NavTypeText = navType switch
                {
                    0 => FindString("MapPage_Nav_Start"),
                    1 => FindString("MapPage_Nav_Gate"),
                    2 => FindString("MapPage_Nav_Capital"),
                    _ => FindString("MapPage_Nav_Bridge"),
                },
                Fuel = fuel,
            });
        }

        NavResult.Clear();
        foreach (var item in result)
        {
            NavResult.Add(item);
        }

        LastPath = path;
        LastWaypointIndices = waypointIndices;

        var gates = result.Count(p => p.NavType == 1);
        var jumps = result.Count(p => p.NavType == 2);
        var bridgeHops = result.Count(p => p.NavType == 3);
        var jumpLy = result.Where(p => p.NavType == 2).Sum(p => p.DistanceLy);
        var fuelTotal = result.Sum(p => p.Fuel);
        NavSummary = string.Format(
            FindString("MapPage_Nav_Summary"),
            result.Count,
            gates,
            jumps,
            bridgeHops,
            jumpLy,
            fuelTotal);
    }

    /// <summary>清除导航结果。</summary>
    public void ClearNavigation()
    {
        NavResult.Clear();
        LastPath = [];
        LastWaypointIndices = [];
        LastNavigationError = null;
        NavSummary = string.Empty;
        OnPropertyChanged(nameof(LastNavigationError));
    }

    /// <summary>
    /// 在游戏中设置航点（自动驾驶）：逐点调 ESI，点与点之间留 <see cref="AutopilotIntervalMs"/> 间隔避免限流；
    /// <paramref name="onProgress"/> 回传 (当前, 总数) 供页面显示进度。
    /// </summary>
    public async Task<bool> SetAutopilotAsync(Action<int, int>? onProgress = null)
    {
        var character = AutopilotCharacter;
        if (character is null)
        {
            LastNavigationError = FindString("MapPage_Error_NoCharacter");
            return false;
        }

        if (LastPath.Count == 0)
        {
            LastNavigationError = FindString("MapPage_Error_NeedRoute");
            return false;
        }

        if (!await CharacterStore.EnsureTokenValidAsync(character))
        {
            LastNavigationError = FindString("MapPage_Error_Token");
            return false;
        }

        try
        {
            var esi = Core.Services.ESIService.GetDefaultESI();
            var auth = Core.Services.ESIService.ToEVEStandardSSO(character);
            var total = LastPath.Count;
            for (var i = 0; i < total; i++)
            {
                onProgress?.Invoke(i + 1, total);
                await esi.UserInterface.SetAutopilotWaypointAsync(auth, false, false, LastPath[i]);
                if (i < total - 1)
                {
                    await Task.Delay(AutopilotIntervalMs);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            LastNavigationError = ex.Message;
            return false;
        }
    }

    // ---------- 一跳覆盖 ----------

    /// <summary>普通（非旗舰）最大跳跃距离输入（光年）——一跳覆盖用旗舰参数，见 <see cref="ShipMaxJump"/>。</summary>
    public async Task<List<MapSolarSystem>> CalcOneJumpCoverAsync(int systemId)
    {
        var maxLy = JumpShip is not null ? ShipMaxJump : MaxLy;
        return await Task.Run(() =>
        {
            try
            {
                return Core.EVEHelpers.ShortestPathHelper.CalOneJumpCover(systemId, maxLy) ?? [];
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                return [];
            }
        });
    }

    /// <summary>一跳覆盖高亮变化（空数组 = 清除）。</summary>
    public void SetCover(int[] systemIds) => CoverChanged?.Invoke(this, systemIds);

    // ---------- 角色标记 ----------

    private readonly HashSet<long> _portraitRequested = [];

    public void OnCharacterLocations(IReadOnlyList<CharacterLocationService.CharacterLocation> locations)
    {
        foreach (var location in locations)
        {
            if (!_portraitRequested.Add(location.Character.CharacterID))
            {
                continue;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var url = GameImageHelper.BuildCharacterPortraitUrl(location.Character.CharacterID, 64);
                    if (url is null)
                    {
                        return;
                    }

                    var bytes = await Core.Helpers.HttpHelper.GetByteArrayAsync(url);
                    if (bytes is not { Length: > 0 })
                    {
                        return;
                    }

                    var bitmap = SKBitmap.Decode(bytes);
                    if (bitmap is not null)
                    {
                        await _dispatcher.BeginInvoke(() => PortraitLoaded?.Invoke(this, (location.Character.CharacterID, bitmap)));
                    }
                }
                catch (Exception ex)
                {
                    Core.Log.Error(ex);
                }
            });
        }
    }

    /// <summary>关闭角色显示时清掉"已下载"标记，下次打开重新取头像。</summary>
    public void ResetPortraits() => _portraitRequested.Clear();

    // ---------- 工具 ----------

    private void AutoFillMaxLyFromShip()
    {
        if (JumpShip is not null && CapitalMode)
        {
            MaxLy = Math.Clamp(Math.Round(ShipMaxJump, 1), 1, 20);
        }
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

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
