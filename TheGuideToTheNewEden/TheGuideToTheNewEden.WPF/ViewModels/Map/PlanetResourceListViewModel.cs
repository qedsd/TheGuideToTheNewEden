using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.WPF.ViewModels.Map;

/// <summary>行星资源清单：星域汇总行。</summary>
public sealed class RegionResourceRow
{
    public string RegionName { get; init; } = string.Empty;
    public long Power { get; init; }
    public long Workforce { get; init; }
    public long MagmaticGas { get; init; }
    public long SuperionicIce { get; init; }
}

/// <summary>行星资源清单：星系汇总行。</summary>
public sealed class SystemResourceRow
{
    public int SystemId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string RegionName { get; init; } = string.Empty;
    public string SecurityText { get; init; } = string.Empty;
    public long Power { get; init; }
    public long Workforce { get; init; }
    public long MagmaticGas { get; init; }
    public long SuperionicIce { get; init; }
}

/// <summary>
/// 行星资源清单（对齐 WinUI <c>PlanetResourcListPage</c>）：星域汇总 / 星系汇总 / 设施升级三个页签。
/// 数据来自本地库聚合（<see cref="Services.Map.MapResourceService"/>）。
/// </summary>
public sealed class PlanetResourceListViewModel : INotifyPropertyChanged
{
    private bool _isLoading;
    private bool _loaded;
    private bool _onlyWithResource = true;
    private string _summary = string.Empty;

    public ObservableCollection<RegionResourceRow> RegionRows { get; } = [];
    public ObservableCollection<SystemResourceRow> SystemRows { get; } = [];
    public ObservableCollection<UpgradeRow> UpgradeRows { get; } = [];

    private List<SystemResourceRow> _allSystemRows = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set => Set(ref _isLoading, value);
    }

    /// <summary>只列出四项资源里至少有一项非 0 的星系。</summary>
    public bool OnlyWithResource
    {
        get => _onlyWithResource;
        set
        {
            if (Set(ref _onlyWithResource, value))
            {
                ApplySystemFilter();
            }
        }
    }

    public string Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>清缓存后重新装载（"重新载入"按钮）。</summary>
    public async Task ReloadAsync()
    {
        Services.Map.MapResourceService.ClearCache();
        _loaded = false;
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        if (_loaded || IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            if (!Services.Map.MapResourceService.IsLoaded)
            {
                var systems = await Task.Run(() => Core.Services.DB.MapSolarSystemService.QueryAll());
                await Services.Map.MapResourceService.LoadAsync(systems);
            }

            var regionDic = await Task.Run(() => Core.Services.DB.MapRegionService.QueryAll().ToDictionary(p => p.RegionID, p => p.RegionName));

            RegionRows.Clear();
            foreach (var region in Services.Map.MapResourceService.RegionResources.Values
                .OrderByDescending(p => p.Power + p.Workforce))
            {
                RegionRows.Add(new RegionResourceRow
                {
                    RegionName = region.Region?.RegionName ?? string.Empty,
                    Power = region.Power,
                    Workforce = region.Workforce,
                    MagmaticGas = region.MagmaticGas,
                    SuperionicIce = region.SuperionicIce,
                });
            }

            _allSystemRows = [.. Services.Map.MapResourceService.SystemResources.Values.Select(p => new SystemResourceRow
            {
                SystemId = p.MapSolarSystem.SolarSystemID,
                Name = p.MapSolarSystem.SolarSystemName ?? string.Empty,
                RegionName = regionDic.TryGetValue(p.MapSolarSystem.RegionID, out var regionName) ? regionName : string.Empty,
                SecurityText = p.MapSolarSystem.Security <= 0 ? "0.0" : p.MapSolarSystem.Security.ToString("0.00"),
                Power = p.Power,
                Workforce = p.Workforce,
                MagmaticGas = p.MagmaticGas,
                SuperionicIce = p.SuperionicIce,
            })];

            ApplySystemFilter();

            UpgradeRows.Clear();
            foreach (var upgrade in Services.Map.MapResourceService.GetUpgrades())
            {
                UpgradeRows.Add(new UpgradeRow(upgrade));
            }

            Summary = string.Format(FindString("MapTool_Resource_Summary"), RegionRows.Count, _allSystemRows.Count, UpgradeRows.Count);
            _loaded = true;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            Summary = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplySystemFilter()
    {
        SystemRows.Clear();
        IEnumerable<SystemResourceRow> rows = _allSystemRows;
        if (OnlyWithResource)
        {
            rows = rows.Where(p => p.Power != 0 || p.Workforce != 0 || p.MagmaticGas != 0 || p.SuperionicIce != 0);
        }

        foreach (var row in rows.OrderByDescending(p => p.Power + p.Workforce))
        {
            SystemRows.Add(row);
        }
    }

    private static string FindString(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;

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
