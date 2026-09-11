using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.ViewModels.Characters;

/// <summary>单个脑插（名称 + 描述，描述用作悬浮提示）。</summary>
public sealed class CloneImplantViewModel
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }
}

/// <summary>单个克隆（当前激活克隆或跳跃克隆）。</summary>
public sealed class CloneItemViewModel
{
    public string LocationName { get; init; } = string.Empty;

    public string? CloneName { get; init; }

    public List<CloneImplantViewModel> Implants { get; init; } = [];

    public int ImplantCount => Implants.Count;

    public bool HasCloneName => !string.IsNullOrWhiteSpace(CloneName);

    public bool HasImplants => Implants.Count > 0;

    /// <summary>没有脑插时显示占位横线。</summary>
    public bool NoImplants => Implants.Count == 0;
}

/// <summary>
/// 克隆页视图模型：与 WinUI3 ClonePage 对齐的汇总信息（基地、上一次变更、克隆数量、上一次远克）
/// 以及当前激活克隆 / 各跳跃克隆的脑插列表。
/// </summary>
public sealed class ClonePageViewModel : INotifyPropertyChanged
{
    private string _homeLocation = "-";
    private string _lastStationChangeText = "-";
    private string _lastCloneJumpText = "-";
    private int _jumpCloneCount;
    private CloneItemViewModel _activeClone = new();

    /// <summary>基地（当前激活克隆所在地点）。</summary>
    public string HomeLocation
    {
        get => _homeLocation;
        private set => Set(ref _homeLocation, value);
    }

    /// <summary>上一次变更基地的时间。</summary>
    public string LastStationChangeText
    {
        get => _lastStationChangeText;
        private set => Set(ref _lastStationChangeText, value);
    }

    /// <summary>上一次远克的时间。</summary>
    public string LastCloneJumpText
    {
        get => _lastCloneJumpText;
        private set => Set(ref _lastCloneJumpText, value);
    }

    /// <summary>克隆数量（含当前激活克隆，与 WinUI 一致）。</summary>
    public int JumpCloneCount
    {
        get => _jumpCloneCount;
        private set => Set(ref _jumpCloneCount, value);
    }

    /// <summary>当前激活克隆（地点取基地，含脑插）。</summary>
    public CloneItemViewModel ActiveClone
    {
        get => _activeClone;
        private set => Set(ref _activeClone, value);
    }

    /// <summary>跳跃克隆列表。</summary>
    public ObservableCollection<CloneItemViewModel> Clones { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>把服务数据映射到界面；集合原地重建以便绑定持续生效。</summary>
    public void Apply(CharacterCloneData data)
    {
        HomeLocation = string.IsNullOrWhiteSpace(data.HomeLocation) ? "-" : data.HomeLocation!;
        LastStationChangeText = FormatDate(data.LastStationChange);
        LastCloneJumpText = FormatDate(data.LastCloneJump);
        JumpCloneCount = data.JumpCloneCount;
        ActiveClone = ToItem(data.ActiveClone);

        Clones.Clear();
        foreach (var clone in data.Clones)
        {
            Clones.Add(ToItem(clone));
        }
    }

    private static CloneItemViewModel ToItem(CloneView? clone)
    {
        if (clone is null)
        {
            return new CloneItemViewModel();
        }

        return new CloneItemViewModel
        {
            LocationName = clone.LocationName,
            CloneName = clone.CloneName,
            Implants = clone.Implants
                .Select(implant => new CloneImplantViewModel
                {
                    Name = implant.Name,
                    Description = implant.Description,
                })
                .ToList(),
        };
    }

    /// <summary>时间统一转本地时间显示；无记录时用横线占位。</summary>
    private static string FormatDate(DateTime? value) =>
        value is null ? "-" : value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
