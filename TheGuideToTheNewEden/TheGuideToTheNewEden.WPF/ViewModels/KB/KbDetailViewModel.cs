using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services.KB;

namespace TheGuideToTheNewEden.WPF.ViewModels.KB;

/// <summary>KB 详情页：受害者信息、价值面板、攻击者列表与货柜。</summary>
public sealed class KbDetailViewModel : INotifyPropertyChanged
{
    private readonly CancellationTokenSource _cts = new();
    private bool _loaded;

    public KbDetailViewModel(KBItemInfo info)
    {
        Info = info;
        KillmailId = (int)info.SKBDetail.KillmailId;
        TimeText = info.SKBDetail.KillmailTime.ToString("yyyy-MM-dd HH:mm:ss");
        Title = info.Victim?.Name ?? info.SKBDetail.Victim?.CharacterId.ToString() ?? KillmailId.ToString();
        SubTitle = BuildSubTitle(info);
        ShipImageUrl = GameImageHelper.BuildTypeImageUrl(info.SKBDetail.Victim?.ShipTypeId ?? 0, 128);

        var zkb = info.SKBDetail.Zkb;
        DroppedValueText = IskFormatHelper.Format(zkb?.DroppedValue ?? 0);
        DestroyedValueText = IskFormatHelper.Format(zkb?.DestroyedValue ?? 0);
        FittedValueText = IskFormatHelper.Format(zkb?.FittedValue ?? 0);
        TotalValueText = IskFormatHelper.Format(zkb?.TotalValue ?? 0);
        PointsText = (zkb?.Points ?? 0).ToString();
        IsSolo = zkb?.Solo ?? false;
        IsNpc = zkb?.Npc ?? false;
        IsAwox = zkb?.Awox ?? false;
        TotalDamage = info.TotalDamage;
    }

    public KBItemInfo Info { get; }

    public int KillmailId { get; }

    public string Title { get; }

    public string SubTitle { get; }

    public string? ShipImageUrl { get; }

    /// <summary>受害者实体（角色 / 军团 / 联盟，点击跳其统计标签；也用作头部身份图）。</summary>
    public IdName? VictimEntity => Info.Victim;

    /// <summary>受害者军团（点击跳其统计标签；为空时头部整块隐藏）。</summary>
    public IdName? VictimCorpEntity => Info.VictimCorporationIdName;

    /// <summary>受害者联盟（点击跳其统计标签；为空时头部整块隐藏）。</summary>
    public IdName? VictimAllianceEntity => Info.VictimAllianceName;

    /// <summary>是否有军团（控制头部军团徽标与链接的显隐）。</summary>
    public bool HasVictimCorp => Info.VictimCorporationIdName is not null;

    /// <summary>是否有联盟（控制头部联盟徽标与链接的显隐）。</summary>
    public bool HasVictimAlliance => Info.VictimAllianceName is not null;

    /// <summary>受害者舰船（点击跳舰船统计标签）。</summary>
    public IdName? ShipEntity => Info.Type is null
        ? null
        : new IdName(Info.Type.TypeID, Info.Type.TypeName, IdName.CategoryEnum.InventoryType);

    /// <summary>星系（点击跳星系统计标签）。</summary>
    public IdName? SystemEntity => Info.SolarSystem is null
        ? null
        : new IdName(Info.SolarSystem.SolarSystemID, Info.SolarSystem.SolarSystemName, IdName.CategoryEnum.SolarSystem);

    /// <summary>星域（点击跳星域统计标签）。</summary>
    public IdName? RegionEntity => Info.Region is null
        ? null
        : new IdName(Info.Region.RegionID, Info.Region.RegionName, IdName.CategoryEnum.Region);

    /// <summary>星系安全等级文本（如 0.5）。</summary>
    public string SystemSecurityText => Info.SolarSystem is null
        ? string.Empty
        : Info.SolarSystem.Security.ToString("0.0");

    public string TimeText { get; }

    public string DroppedValueText { get; }

    public string DestroyedValueText { get; }

    public string FittedValueText { get; }

    public string TotalValueText { get; }

    public string PointsText { get; }

    public bool IsSolo { get; }

    public bool IsNpc { get; }

    public bool IsAwox { get; }

    public int TotalDamage { get; }

    /// <summary>攻击者（按伤害降序）。</summary>
    public ObservableCollection<AttackerRow> Attackers { get; } = [];

    /// <summary>货柜（层级已展开为带缩进的平铺行）。</summary>
    public ObservableCollection<CargoRow> Cargo { get; } = [];

    public bool HasCargo => Cargo.Count > 0;

    private bool _isLoading;

    /// <summary>
    /// 是否正在页内加载（攻击者/货柜）。详情页的局部等待遮罩绑这里——
    /// 必须走 <see cref="OnPropertyChanged(string?)"/>，否则界面收不到通知，遮罩永远不显示。
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value)
            {
                return;
            }

            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public async Task LoadAsync()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        // 先亮遮罩再干活：本方法由 Loaded 触发，早于首帧渲染，页面一出现就是"加载中"
        IsLoading = true;

        try
        {
            var attackers = await ZkbQueryService.BuildAttackerInfosAsync(Info.SKBDetail, _cts.Token);
            Attackers.Clear();
            for (var i = 0; i < attackers.Count; i++)
            {
                var row = new AttackerRow(attackers[i])
                {
                    // 列表已按伤害降序：第一条（伤害 > 0）即最高伤害
                    IsTopDamage = i == 0 && attackers[i].Attacker.DamageDone > 0,
                };
                Attackers.Add(row);
            }

            var cargo = await ZkbQueryService.BuildCargoInfosAsync(Info.SKBDetail, _cts.Token);
            Cargo.Clear();
            foreach (var row in Flatten(cargo, 0))
            {
                Cargo.Add(row);
            }

            OnPropertyChanged(nameof(HasCargo));
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

    private static IEnumerable<CargoRow> Flatten(IEnumerable<CargoItemInfo> items, int level)
    {
        foreach (var item in items)
        {
            yield return new CargoRow
            {
                Name = new string(' ', level * 3) + (item.Type?.TypeName ?? item.CargoItem.ItemTypeId.ToString()),
                Destroyed = item.CargoItem.QuantityDestroyed,
                Dropped = item.CargoItem.QuantityDropped,
            };

            if (item.SubItems is { Count: > 0 })
            {
                foreach (var child in Flatten(item.SubItems, level + 1))
                {
                    yield return child;
                }
            }
        }
    }

    private static string BuildSubTitle(KBItemInfo info)
    {
        var ship = info.Type?.TypeName;
        var system = info.SolarSystem?.SolarSystemName;
        var region = info.Region?.RegionName;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(ship))
        {
            parts.Add(ship);
        }

        if (!string.IsNullOrWhiteSpace(system))
        {
            var sec = info.SolarSystem is null ? null : $"{info.SolarSystem.Security:0.0}";
            parts.Add(string.IsNullOrWhiteSpace(sec) ? system : $"{system} ({sec})");
        }

        if (!string.IsNullOrWhiteSpace(region))
        {
            parts.Add(region);
        }

        return string.Join(" · ", parts);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>攻击者列表的一行（附加"最后一击 / 最高伤害"标记与伤害占比文本）。</summary>
public sealed class AttackerRow
{
    public AttackerRow(AttackerInfo info)
    {
        Info = info;
        IsFinalBlow = info.Attacker.FinalBlow;
        DamagePercentText = $"{info.DamageRatio * 100:0.0}%";
    }

    public AttackerInfo Info { get; }

    /// <summary>参与者角色（点击跳其统计标签）。</summary>
    public IdName? CharacterEntity => Info.CharacterName;

    /// <summary>参与者势力：联盟优先、否则军团（点击跳其统计标签）。</summary>
    public IdName? FactionEntity => Info.AllianceName ?? Info.CorpName;

    /// <summary>参与者舰船（点击跳舰船统计标签）。</summary>
    public IdName? ShipEntity => Info.Ship is null
        ? null
        : new IdName(Info.Ship.TypeID, Info.Ship.TypeName, IdName.CategoryEnum.InventoryType);

    public bool IsFinalBlow { get; }

    /// <summary>是否最高伤害（由列表构建方设置）。</summary>
    public bool IsTopDamage { get; set; }

    public string DamagePercentText { get; }
}

/// <summary>货柜的一行（已按层级加缩进）。</summary>
public sealed class CargoRow
{
    public string Name { get; init; } = string.Empty;

    public long Destroyed { get; init; }

    public long Dropped { get; init; }
}
