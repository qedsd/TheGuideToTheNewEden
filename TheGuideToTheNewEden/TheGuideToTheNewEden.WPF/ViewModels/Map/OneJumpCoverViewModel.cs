using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WPF.Views.UserControls.Map;

namespace TheGuideToTheNewEden.WPF.ViewModels.Map;

/// <summary>一跳覆盖结果行。</summary>
public sealed class CoverItem
{
    public int No { get; set; }
    public MapSystemNode Node { get; init; } = null!;
    public string Name => Node.Name;
    public string RegionName => Node.RegionName;
    public string SecurityText => Node.Security <= 0 ? "0.0" : Node.Security.ToString("0.00");
    /// <summary>距中心星系的 3D 距离（光年）。</summary>
    public double DistanceLy { get; init; }
    /// <summary>本跳预计燃料。</summary>
    public double Fuel { get; init; }
    public string SovName { get; init; } = string.Empty;
}

/// <summary>
/// 一跳覆盖工具：给定中心星系与旗舰（型号 + JDC/JFC/货舰技能）算出最大跳跃光年，
/// 调 <c>ShortestPathHelper.CalOneJumpCover</c> 得到一跳可达的全部星系（仅 00），
/// 按距离排序并算燃料；可把结果圈回星图。
/// </summary>
public sealed class OneJumpCoverViewModel : INotifyPropertyChanged
{
    private static readonly int StrategicFreighterGroupId = 1089;

    private MapSystemNode? _center;
    private CapitalJumpShipInfo? _ship;
    private int _jdc = 4;
    private int _jfc = 4;
    private int _jumpFreighters = 4;
    private bool _isBusy;
    private string _summary = string.Empty;

    public ObservableCollection<CapitalJumpShipInfo> JumpShips { get; } = [];
    public ObservableCollection<CoverItem> Items { get; } = [];

    public OneJumpCoverViewModel()
    {
        foreach (var ship in Core.EVEHelpers.CapitalJumpShipInfoHelper.GetInfos() ?? [])
        {
            JumpShips.Add(ship);
        }

        _ship = JumpShips.FirstOrDefault();
    }

    public MapSystemNode? Center
    {
        get => _center;
        private set
        {
            if (Set(ref _center, value))
            {
                OnPropertyChanged(nameof(CenterText));
            }
        }
    }

    public string CenterText => Center is null
        ? FindString("MapTool_Cover_NoCenter")
        : $"{Center.RegionName} · {Center.Name}  {(Center.Security <= 0 ? "0.0" : Center.Security.ToString("0.00"))}";

    public CapitalJumpShipInfo? Ship
    {
        get => _ship;
        set
        {
            if (Set(ref _ship, value))
            {
                OnPropertyChanged(nameof(ShowJumpFreighters));
                OnPropertyChanged(nameof(MaxJumpText));
                OnPropertyChanged(nameof(PerLyFuelText));
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
                OnPropertyChanged(nameof(MaxJumpText));
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
                OnPropertyChanged(nameof(PerLyFuelText));
            }
        }
    }

    public int JumpFreighters
    {
        get => _jumpFreighters;
        set
        {
            if (Set(ref _jumpFreighters, Math.Clamp(value, 0, 5)))
            {
                OnPropertyChanged(nameof(PerLyFuelText));
            }
        }
    }

    public bool ShowJumpFreighters => Ship?.GroupID == StrategicFreighterGroupId;

    public double MaxJump => Ship is null ? 0 : Ship.MaxLY + Ship.MaxLY * 0.2 * JumpDriveCalibration;

    public string MaxJumpText => MaxJump.ToString("N2");

    public double PerLyFuel
    {
        get
        {
            if (Ship is null)
            {
                return 0;
            }

            var baseFuel = Ship.PerLYFuel;
            if (Ship.GroupID == StrategicFreighterGroupId)
            {
                baseFuel -= baseFuel * 0.1 * JumpFreighters;
            }

            return baseFuel - baseFuel * 0.1 * JumpFuelConservation;
        }
    }

    public string PerLyFuelText => PerLyFuel.ToString("N2");

    public bool IsBusy
    {
        get => _isBusy;
        private set => Set(ref _isBusy, value);
    }

    public string Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
    }

    /// <summary>覆盖结果变化（星系 Id 数组，空数组 = 清除圈）。</summary>
    public event EventHandler<int[]>? CoverChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetCenter(MapSystemNode? node) => Center = node;

    /// <summary>计算一跳覆盖；成功返回 true。</summary>
    public async Task<bool> ComputeAsync()
    {
        if (Center is null)
        {
            Summary = FindString("MapTool_Cover_NoCenter");
            return false;
        }

        IsBusy = true;
        try
        {
            var centerId = Center.Id;
            var maxLy = MaxJump;
            var perLyFuel = PerLyFuel;
            var systems = await Task.Run(() =>
            {
                try
                {
                    return Core.EVEHelpers.ShortestPathHelper.CalOneJumpCover(centerId, maxLy) ?? [];
                }
                catch (Exception ex)
                {
                    Core.Log.Error(ex);
                    return [];
                }
            });

            _positionDic = Core.EVEHelpers.SolarSystemPosHelper.PositionDic;
            var regionDic = Core.Services.DB.MapRegionService.QueryAll().ToDictionary(p => p.RegionID, p => p.RegionName);

            var rows = new List<CoverItem>(systems.Count);
            foreach (var system in systems)
            {
                var distance = GetDistanceLy(centerId, system.SolarSystemID);
                rows.Add(new CoverItem
                {
                    Node = new MapSystemNode
                    {
                        Id = system.SolarSystemID,
                        Name = system.SolarSystemName ?? string.Empty,
                        RegionId = system.RegionID,
                        RegionName = regionDic.TryGetValue(system.RegionID, out var regionName) ? regionName : string.Empty,
                        Security = system.Security,
                    },
                    DistanceLy = distance,
                    Fuel = perLyFuel * distance,
                    SovName = Services.Map.SovService.GetSovName(system.SolarSystemID),
                });
            }

            Items.Clear();
            var no = 1;
            foreach (var row in rows.OrderBy(p => p.DistanceLy))
            {
                row.No = no++;
                Items.Add(row);
            }

            Summary = string.Format(FindString("MapTool_Cover_Summary"), Center.Name, MaxJumpText, Items.Count);
            CoverChanged?.Invoke(this, [.. Items.Select(p => p.Node.Id)]);
            return Items.Count > 0;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            Summary = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static Dictionary<int, Core.Models.Map.SolarSystemPosition> _positionDic = [];

    private static double GetDistanceLy(int fromId, int toId)
    {
        if (!_positionDic.TryGetValue(fromId, out var from) || !_positionDic.TryGetValue(toId, out var to))
        {
            return 0;
        }

        return Math.Sqrt(Math.Pow(from.X - to.X, 2) + Math.Pow(from.Y - to.Y, 2) + Math.Pow(from.Z - to.Z, 2)) / 9460730472580800;
    }

    public void Clear()
    {
        Items.Clear();
        Summary = string.Empty;
        CoverChanged?.Invoke(this, []);
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
