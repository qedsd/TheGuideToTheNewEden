using System.Collections.Generic;
using System.Linq;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.WPF.Models.Wormhole;

/// <summary>
/// 洞系搜索列表条目（DB Wormhole 的展示包装）。
/// 注：本文件命名空间以 <c>Wormhole</c> 结尾，与 Core 的 <c>Wormhole</c> 类型同名，
/// 因此文件内引用 Core 类型一律写全限定名。
/// </summary>
public sealed class WormholeListItem
{
    public WormholeListItem(TheGuideToTheNewEden.Core.DBModels.Wormhole wormhole)
    {
        Wormhole = wormhole;
    }

    public TheGuideToTheNewEden.Core.DBModels.Wormhole Wormhole { get; }

    public int Id => Wormhole.Id;

    public string Name => Wormhole.Name;

    public int Class => Wormhole.Class;

    public string ClassName => FindString($"WormholePage_Class_{Wormhole.Class}");

    public int Phenomena => Wormhole.Phenomena;

    public string PhenomenaName => FindString($"WormholePage_Phenomena_{Wormhole.Phenomena}");

    private static string FindString(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;
}

/// <summary>
/// 洞口按钮/行通用的"通往"文案工具：Destination/AppearsIn 是逗号分隔的等级代码（1-6、12-18、100-102）。
/// </summary>
public static class WormholeFormat
{
    public static string DestinationCodesToText(string? codes)
    {
        if (string.IsNullOrWhiteSpace(codes))
        {
            return string.Empty;
        }

        var parts = codes
            .Split(',')
            .Where(p => int.TryParse(p.Trim(), out _))
            .Select(p => FindString($"WormholePage_Class_{int.Parse(p.Trim())}"));
        return string.Join(" · ", parts);
    }

    /// <summary>质量人类化（kg）：≥10 亿显示 x.xxB，≥100 万显示 xxxM，其余 N0。</summary>
    public static string MassToText(long massKg)
    {
        if (massKg >= 1_000_000_000)
        {
            return $"{massKg / 1_000_000_000d:0.##} B kg";
        }

        if (massKg >= 1_000_000)
        {
            return $"{massKg / 1_000_000d:0.#} M kg";
        }

        return $"{massKg:N0} kg";
    }

    /// <summary>质量全量（计算器与表格用）：千分位 + kg。</summary>
    public static string MassToFullText(double massKg) => $"{massKg:N0} kg";

    private static string FindString(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;
}

/// <summary>
/// 洞口按钮（洞系详情卡里的永联/漫游洞）：代号 + 通往摘要，点击弹详情窗。
/// </summary>
public sealed class PortalButtonModel
{
    public PortalButtonModel(WormholePortal portal)
    {
        Portal = portal;
    }

    public WormholePortal Portal { get; }

    public string Name => Portal.Name;

    public string DestinationsText => WormholeFormat.DestinationCodesToText(Portal.Destination);
}

/// <summary>天体条目（星球/月球/其他）。</summary>
public sealed class StellarItem
{
    public StellarItem(MapDenormalize item, string typeName)
    {
        ItemName = item.ItemName;
        TypeName = typeName;
    }

    public string ItemName { get; }

    public string TypeName { get; }
}

/// <summary>
/// 洞系详情（选中某个虫洞星系后的完整展示模型，对应 WinUI 的 <c>WormholeDetail</c>）。
/// </summary>
public sealed class WormholeDetail
{
    public static WormholeDetail Create(TheGuideToTheNewEden.Core.DBModels.Wormhole wormhole, List<WormholePortal> statics, List<WormholePortal> wanderings, List<StellarItem> planets, List<StellarItem> moons, List<StellarItem> others)
    {
        return new WormholeDetail
        {
            Wormhole = wormhole,
            StaticsPortals = statics.Select(p => new PortalButtonModel(p)).ToList(),
            WanderingPortals = wanderings.Select(p => new PortalButtonModel(p)).ToList(),
            Planets = planets,
            Moons = moons,
            Others = others,
        };
    }

    public TheGuideToTheNewEden.Core.DBModels.Wormhole Wormhole { get; private set; } = null!;

    public int Id => Wormhole.Id;

    public string Name => Wormhole.Name;

    public string ClassName => FindString($"WormholePage_Class_{Wormhole.Class}");

    public string PhenomenaName => FindString($"WormholePage_Phenomena_{Wormhole.Phenomena}");

    /// <summary>天象对游戏的影响简介（新增：WinUI 版没有）。</summary>
    public string PhenomenaDescription => FindString($"WormholePage_PhenomenaDesc_{Wormhole.Phenomena}");

    public string ClassDescription => FindString($"WormholePage_ClassDesc_{Wormhole.Class}");

    public List<PortalButtonModel> StaticsPortals { get; private set; } = [];

    public List<PortalButtonModel> WanderingPortals { get; private set; } = [];

    public List<StellarItem> Planets { get; private set; } = [];

    public List<StellarItem> Moons { get; private set; } = [];

    public List<StellarItem> Others { get; private set; } = [];

    public bool HasStatics => StaticsPortals.Count > 0;

    public bool HasWanderings => WanderingPortals.Count > 0;

    private static string FindString(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;
}

/// <summary>洞口库表格行（全部 WormholePortal 的展示包装，带筛选排序用的原始数值）。</summary>
public sealed class PortalRow
{
    public PortalRow(WormholePortal portal)
    {
        Portal = portal;
    }

    public WormholePortal Portal { get; }

    public string Name => Portal.Name;

    public string DestinationsText => WormholeFormat.DestinationCodesToText(Portal.Destination);

    public string AppearsInText => WormholeFormat.DestinationCodesToText(Portal.AppearsIn);

    /// <summary>用于"通往空间"筛选的等级代码集合（无则空集）。</summary>
    public HashSet<int> DestinationCodes { get; private set; } = [];

    public PortalRow WithDestinationCodes()
    {
        DestinationCodes = (Portal.Destination ?? string.Empty)
            .Split(',')
            .Where(p => int.TryParse(p.Trim(), out _))
            .Select(p => int.Parse(p.Trim()))
            .ToHashSet();
        return this;
    }

    public float Lifetime => Portal.Lifetime;

    public string LifetimeText => $"{Portal.Lifetime:0.#} h";

    public long MaxMassPerJump => Portal.MaxMassPerJump;

    public string MaxMassPerJumpText => WormholeFormat.MassToText(Portal.MaxMassPerJump);

    public long TotalJumpMass => Portal.TotalJumpMass;

    public string TotalJumpMassText => WormholeFormat.MassToText(Portal.TotalJumpMass);

    public long MassRegen => Portal.MassRegen;

    public string MassRegenText => WormholeFormat.MassToText(Portal.MassRegen);

    public string RespawnText => string.IsNullOrEmpty(Portal.Respawn)
        ? FindString("WormholePage_Portal_Respawn_Wandering")
        : FindString(Portal.Respawn == "Static" ? "WormholePage_Portal_Respawn_Static" : "WormholePage_Portal_Respawn_Wandering");

    public string NoteText
    {
        get
        {
            var notes = new[] { Portal.MaxMassPerJumpNote, Portal.TotalJumpMassNote }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!.Trim())
                .Distinct();
            return string.Join(" ", notes);
        }
    }

    private static string FindString(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;
}

/// <summary>ZKB 最活跃军团/联盟条目。</summary>
public sealed class ActiveEntityItem
{
    public string Name { get; init; } = string.Empty;

    /// <summary>军团/联盟 ID（头像与跳 KB 用；0 = 未知）。</summary>
    public int EntityId { get; init; }

    public bool IsAlliance { get; init; }

    public string ImageUrl { get; init; } = string.Empty;

    public int Count { get; init; }

    /// <summary>最近一次参与击杀距今天数。</summary>
    public double LastActiveDays { get; init; }

    /// <summary>最近活跃文案（≥1 天显示"N.N 天前"，否则"N.N 小时前"）。</summary>
    public string LastActiveText
    {
        get
        {
            if (LastActiveDays >= 1)
            {
                return $"{LastActiveDays:N1} {FindString("WormholePage_ZKB_DaysAgo")}";
            }

            var hours = LastActiveDays * 24;
            if (hours >= 1)
            {
                return $"{hours:N1} {FindString("WormholePage_ZKB_HoursAgo")}";
            }

            return FindString("WormholePage_ZKB_ActiveNow");
        }
    }

    private static string FindString(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;
}

/// <summary>ZKB 热门船型（被击毁船型统计）。</summary>
public sealed class ShipTypeStatItem
{
    public string Name { get; init; } = string.Empty;

    public int TypeId { get; init; }

    public string ImageUrl { get; init; } = string.Empty;

    public int Count { get; init; }
}

/// <summary>过洞计算器的船型选项（SDE 实际质量）。</summary>
public sealed class ShipMassOption
{
    public string Name { get; init; } = string.Empty;

    /// <summary>类别说明（如"战列舰 · Dominix 实测质量"）。</summary>
    public string Category { get; init; } = string.Empty;

    public double Mass { get; init; }

    public int TypeId { get; init; }

    public string Display => $"{Name}（{Category}）";
}
