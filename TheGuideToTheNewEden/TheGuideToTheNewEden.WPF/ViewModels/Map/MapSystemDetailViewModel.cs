using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheGuideToTheNewEden.Core.Models.PlanetResources;
using TheGuideToTheNewEden.WPF.Views.UserControls.Map;

namespace TheGuideToTheNewEden.WPF.ViewModels.Map;

/// <summary>设施升级行（带"当前星系资源是否够用"标记）。</summary>
public sealed class UpgradeRow
{
    private readonly long _systemPower;

    public UpgradeRow(Upgrade upgrade, long systemPower = 0)
    {
        Upgrade = upgrade;
        _systemPower = systemPower;
    }

    public Upgrade Upgrade { get; }
    public string Name => Upgrade.Name ?? Upgrade.Id.ToString();
    public long Power => Upgrade.Power;
    public long Workforce => Upgrade.Workforce;
    public long SuperionicIce => Upgrade.SuperionicIce;
    public long MagmaticGas => Upgrade.MagmaticGas;

    /// <summary>该星系产能是否满足此项升级（无上下文时视为满足）。</summary>
    public bool Fit => _systemPower <= 0 || Upgrade.Power <= _systemPower;
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
/// 星系详情（对齐 WinUI <c>MapSystemDetailPage</c>）：左侧固定信息栏 + 五个页签
/// （统计 / 设施升级 / 行星资源 / 天体 / 邻接）。
/// 数据来源：本地库（行星资源、天体、邻接）+ ESI 统计（<see cref="MapPageViewModel.GetSystemStat"/>）+ 主权缓存。
/// </summary>
public sealed class MapSystemDetailViewModel : INotifyPropertyChanged
{
    private readonly MapPageViewModel _map;

    private bool _isLoading;
    private bool _loaded;

    public MapSystemDetailViewModel(MapSystemNode node, MapPageViewModel map)
    {
        Node = node;
        _map = map;
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

    public string ShipKillsText => _shipKills.ToString("N0");
    public string NpcKillsText => _npcKills.ToString("N0");
    public string PodKillsText => _podKills.ToString("N0");
    public string JumpsText => _jumps.ToString("N0");

    public ObservableCollection<SystemPlanetRow> PlanetRows { get; } = [];
    public ObservableCollection<UpgradeRow> UpgradeRows { get; } = [];
    public ObservableCollection<CelestialRow> CelestialRows { get; } = [];
    public ObservableCollection<NeighborRow> NeighborRows { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set => Set(ref _isLoading, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>装载五个页签的数据（幂等）。</summary>
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
            // 单独 try/catch：Core 这一路依赖本地库，万一异常也只丢"四项资源"，不能让五个页签全都加载不出来。
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

            UpgradeRows.Clear();
            foreach (var upgrade in Services.Map.MapResourceService.GetUpgrades())
            {
                UpgradeRows.Add(new UpgradeRow(upgrade, Power));
            }

            BuildNeighbors();
            _loaded = true;
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
