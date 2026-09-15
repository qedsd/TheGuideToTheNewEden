using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json;
using SkiaSharp;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Models.Map;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.Services.Map;
using TheGuideToTheNewEden.WPF.Views.UserControls.Map;

namespace TheGuideToTheNewEden.WPF.ViewModels.Map;

/// <summary>情报流条目。</summary>
public sealed class IntelMsgItem
{
    public DateTime TimeUtc { get; init; }
    public string TimeText { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public int SystemId { get; init; }
    public string SystemName { get; init; } = string.Empty;
    public string Listener { get; init; } = string.Empty;
    public bool IsClear { get; init; }
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
    /// <summary>0 起点 / 1 星门 / 2 旗舰跳。</summary>
    public int NavType { get; init; }
    public string NavTypeText { get; init; } = string.Empty;
}

/// <summary>
/// 星图页 ViewModel：数据装载（星系/星门/ESI 统计）、搜索、着色模式、
/// 情报模式（ChannelIntelManager "无视跳数"旁路 + 过期清理）、角色标记、导航计算。
/// 画布交互桥接由 MapPage 代码后置完成。
/// </summary>
public sealed class MapPageViewModel : INotifyPropertyChanged
{
    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;

    /// <summary>情报消息上限（超出裁剪最旧的）。</summary>
    private const int MaxIntelMsgs = 300;

    private bool _isLoading;
    private bool _loaded;
    private string _searchText = string.Empty;
    private MapColorMode _colorMode = MapColorMode.Security;
    private bool _showCharacters;
    private bool _intelRunning;
    private MapSystemNode? _selectedSystem;
    private double _selectedKills;
    private double _selectedJumps;
    private double _maxLy = 6;
    private bool _capitalMode;
    private bool _useGates = true;
    private AuthorizedCharacterData? _autopilotCharacter;
    private string _intelExcludeKeywords = string.Empty;
    private string _intelIncludeKeywords = string.Empty;

    private Dictionary<int, double> _killValues = [];
    private Dictionary<int, double> _jumpValues = [];
    private double _killMax;
    private double _jumpMax;
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

    public bool IntelRunning
    {
        get => _intelRunning;
        private set => Set(ref _intelRunning, value);
    }

    /// <summary>是否有运行中的预警会话可接入情报。</summary>
    public bool IntelAvailable => ChannelIntelManager.Current.GetActiveListeners().Count > 0;

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
    }

    public string SelectedSecurityText => SelectedSystem is null
        ? string.Empty
        : (SelectedSystem.Security <= 0 ? "0.0" : SelectedSystem.Security.ToString("0.0"));

    public string SelectedInfoText => SelectedSystem is null
        ? string.Empty
        : $"{SelectedSystem.RegionName} · {SelectedSystem.Name}";

    public string SelectedKillsText => _selectedKills.ToString("N0");
    public string SelectedJumpsText => _selectedJumps.ToString("N0");

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

    public event EventHandler<MapColorMode>? ColorModeChanged;
    public event EventHandler<bool>? ShowCharactersChanged;

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
            var nodes = await Task.Run(BuildNodes);
            var links = await Task.Run(BuildLinks);
            await setData(nodes, links);

            // 区域列表（非特殊星域，按名称排序）
            var regions = await Task.Run(() => Core.Services.DB.MapRegionService.QueryAll()
                .Where(p => !p.IsSpecial())
                .OrderBy(p => p.RegionName)
                .ToList());
            Regions.Clear();
            foreach (var region in regions)
            {
                Regions.Add(region);
            }

            // 已授权角色（导航"在游戏中设置航点"用）
            var characters = await _dispatcher.InvokeAsync(() => CharacterStore.Characters.ToList());
            AutopilotCharacters.Clear();
            foreach (var character in characters)
            {
                AutopilotCharacters.Add(character);
            }
            AutopilotCharacter = AutopilotCharacters.FirstOrDefault();

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

    private static List<MapSystemNode> BuildNodes()
    {
        // 与 SolarSystemPosHelper 一致：过滤特殊星系、Y 轴翻转（游戏原点左下 → 屏幕左上）
        var systems = Core.Services.DB.MapSolarSystemService.QueryAll().Where(p => !p.IsSpecial()).ToList();
        var regions = Core.Services.DB.MapRegionService.QueryAll().ToDictionary(p => p.RegionID, p => p.RegionName);
        var maxY = systems.Max(p => p.Y2);
        var nodes = new List<MapSystemNode>(systems.Count);
        foreach (var system in systems)
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
                var killsResp = await esi.Universe.GetSystemKillsAsync();
                if (killsResp?.Model is not null)
                {
                    foreach (var k in killsResp.Model)
                    {
                        kills[(int)k.SystemId] = k.ShipKills;
                    }
                }
                else
                {
                    Core.Log.Error("星图：GetSystemKillsAsync 失败");
                }

                var jumpsResp = await esi.Universe.GetSystemJumpsAsync();
                if (jumpsResp?.Model is not null)
                {
                    foreach (var j in jumpsResp.Model)
                    {
                        jumps[(int)j.SystemId] = j.ShipJumps;
                    }
                }
                else
                {
                    Core.Log.Error("星图：GetSystemJumpsAsync 失败");
                }
                return (kills, jumps);
            });

            _killValues = result.kills;
            _jumpValues = result.jumps;
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

    public event EventHandler? StatisticsLoaded;

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

    /// <summary>把 ESI 统计写入选中星系信息。</summary>
    public void RefreshSelectedInfo() => UpdateSelectedStats();

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

    // ---------- 情报模式 ----------

    public void RefreshIntelAvailability()
    {
        OnPropertyChanged(nameof(IntelAvailable));
        ActiveListeners.Clear();
        foreach (var listener in ChannelIntelManager.Current.GetActiveListeners())
        {
            ActiveListeners.Add(listener);
        }
    }

    public void StartIntel()
    {
        if (IntelRunning || !IntelAvailable)
        {
            return;
        }

        // 载入/合并情报配置（与 WinUI 共用 MapSettings.json）
        var config = MapSettingService.GetIntel();
        IntelExcludeKeywords = config.Exclusions is { Count: > 0 } ? string.Join(' ', config.Exclusions.Select(p => p.Name)) : IntelExcludeKeywords;
        IntelIncludeKeywords = config.Inclusions is { Count: > 0 } ? string.Join(' ', config.Inclusions.Select(p => p.Name)) : IntelIncludeKeywords;

        ChannelIntelManager.Current.OnIgnoreJumpsIntelUpdate += Intel_OnIgnoreJumpsIntelUpdate;
        ChannelIntelManager.Current.ListenChannelIntel(ChannelIntelManager.Current.GetActiveListeners());
        IntelRunning = true;

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
        IntelRunning = false;
        _intelExpiryTimer?.Stop();
        _intelExpiryTimer = null;
        IntelMessages.Clear();
        IntelMarkersChanged?.Invoke(this, []);
    }

    /// <summary>情报红圈数据变化（页面据此刷新画布覆盖层）。</summary>
    public event EventHandler<IReadOnlyList<IntelMarker>>? IntelMarkersChanged;

    private void Intel_OnIgnoreJumpsIntelUpdate(object sender, IEnumerable<EarlyWarningContent> contents)
    {
        var items = contents as IReadOnlyList<EarlyWarningContent> ?? contents.ToList();
        _ = _dispatcher.BeginInvoke(() =>
        {
            var exclude = SplitKeywords(IntelExcludeKeywords);
            var include = SplitKeywords(IntelIncludeKeywords);
            foreach (var content in items)
            {
                if (content.IntelType == IntelChatType.Ignore)
                {
                    continue;
                }

                if (include.Count > 0 && !include.Any(content.Content.Contains))
                {
                    continue;
                }
                if (exclude.Any(content.Content.Contains))
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
                }

                IntelMessages.Insert(0, item);
                if (IntelMessages.Count > MaxIntelMsgs)
                {
                    IntelMessages.RemoveAt(IntelMessages.Count - 1);
                }
            }

            IntelMarkersChanged?.Invoke(this, BuildIntelMarkers());
        });
    }

    private void IntelExpiry_Tick(object? sender, EventArgs e)
    {
        var config = MapSettingService.Value.Intel;
        var channelSeconds = config.ChannelDuration > 0 ? config.ChannelDuration : 1200;
        var deadline = DateTime.UtcNow.AddSeconds(-channelSeconds);
        var removed = false;
        for (var i = IntelMessages.Count - 1; i >= 0; i--)
        {
            if (IntelMessages[i].TimeUtc < deadline)
            {
                IntelMessages.RemoveAt(i);
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
        foreach (var group in IntelMessages.Where(p => !p.IsClear).GroupBy(p => p.SystemId))
        {
            markers.Add(new IntelMarker { SystemId = group.Key, Weight = group.Count() });
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

    /// <summary>导航完成（页面据此画航线）。</summary>
    public event EventHandler? NavigationCompleted;

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
        var maxLy = MaxLy;

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
                            waypoints[i], waypoints[i + 1], maxLy, useGates, avoid, 0);
                    }
                    else
                    {
                        segment = Core.EVEHelpers.ShortestPathHelper.CalStargatePath(
                            waypoints[i], waypoints[i + 1], avoid, null);
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

        BuildNavResult(path);
        NavigationCompleted?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>航线（星系 Id 列表）与关键航点下标。</summary>
    public IReadOnlyList<int> LastPath { get; private set; } = [];
    public IReadOnlyList<int> LastWaypointIndices { get; private set; } = [];

    private void BuildNavResult(List<int> path)
    {
        var positionDic = Core.EVEHelpers.SolarSystemPosHelper.PositionDic;
        var waypointIds = Waypoints.Select(p => p.Id).ToHashSet();
        var result = new List<NavResultItem>(path.Count);
        var waypointIndices = new List<int>();
        double ly = 0;
        for (var i = 0; i < path.Count; i++)
        {
            var id = path[i];
            Canvas.TryGetNode(id, out var node); // 页面已注入
            if (node is null)
            {
                continue;
            }

            var navType = 0;
            if (i > 0)
            {
                var prev = positionDic.TryGetValue(path[i - 1], out var p1) ? p1 : null;
                var curr = positionDic.TryGetValue(id, out var p2) ? p2 : null;
                if (prev is not null && curr is not null)
                {
                    ly = Math.Sqrt(Math.Pow(prev.X - curr.X, 2) + Math.Pow(prev.Y - curr.Y, 2) + Math.Pow(prev.Z - curr.Z, 2)) / 9460730472580800;
                }

                navType = 2;
                if (prev is not null && prev.JumpTo is { Count: > 0 } && prev.JumpTo.Contains(id))
                {
                    navType = 1;
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
                    _ => FindString("MapPage_Nav_Capital"),
                },
            });
        }

        NavResult.Clear();
        foreach (var item in result)
        {
            NavResult.Add(item);
        }
        LastPath = path;
        LastWaypointIndices = waypointIndices;
    }

    /// <summary>清除导航结果。</summary>
    public void ClearNavigation()
    {
        NavResult.Clear();
        LastPath = [];
        LastWaypointIndices = [];
        LastNavigationError = null;
        OnPropertyChanged(nameof(LastNavigationError));
    }

    // 页面注入的画布引用（用于导航结果解析节点；仅在 UI 线程使用）
    public Views.UserControls.Map.StarMapCanvas Canvas { get; set; } = null!;

    /// <summary>在游戏中设置航点（自动驾驶）。</summary>
    public async Task<bool> SetAutopilotAsync()
    {
        var character = AutopilotCharacter;
        if (character is null || LastPath.Count == 0)
        {
            return false;
        }

        if (!await CharacterStore.EnsureTokenValidAsync(character))
        {
            return false;
        }

        try
        {
            var esi = Core.Services.ESIService.GetDefaultESI();
            var auth = Core.Services.ESIService.ToEVEStandardSSO(character);
            foreach (var id in LastPath)
            {
                await esi.UserInterface.SetAutopilotWaypointAsync(auth, false, false, id);
            }
            return true;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return false;
        }
    }

    // ---------- 角色标记 ----------

    public void OnCharacterLocations(IReadOnlyList<CharacterLocationService.CharacterLocation> locations)
    {
        foreach (var location in locations)
        {
            if (!_portraitDownloaded.Add(location.Character.CharacterID))
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

    public event EventHandler<(long CharacterId, SKBitmap Bitmap)>? PortraitLoaded;
    private readonly HashSet<long> _portraitDownloaded = [];

    // ---------- 工具 ----------

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
