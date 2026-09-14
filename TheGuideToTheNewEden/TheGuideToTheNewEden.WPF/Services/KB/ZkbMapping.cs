using TheGuideToTheNewEden.Core.DBModels;
using ZKB.NET;
using ZKB.NET.Models.Statistics;

namespace TheGuideToTheNewEden.WPF.Services.KB;

/// <summary>
/// ZKB 各套"实体类型"枚举之间的<b>唯一定义与唯一映射入口</b>。
///
/// WinUI 版把同一套映射分散写在 <c>KBNavigationService</c>（两处 switch）、<c>ZKBHomePage</c>（搜索）、
/// 各 <c>Statist*ViewModel</c> 里，新增一种实体类型时极易漏改；这里收敛成一处，
/// 并补齐 ZKB 网页地址的正确段名（WinUI 用 <c>type[..^2]</c> 去 "ID" 后缀，会把 ShipTypeID
/// 拼成 <c>/shipType/</c>、SolarSystemID 拼成 <c>/solarSystem/</c>）。
/// </summary>
public static class ZkbMapping
{
    /// <summary>支持 ZKB 查询的本地类别（其余类别——星座/空间站/结构——没有对应的 ZKB 实体页）。</summary>
    public static readonly IdName.CategoryEnum[] SupportedCategories =
    [
        IdName.CategoryEnum.Character,
        IdName.CategoryEnum.Corporation,
        IdName.CategoryEnum.Alliance,
        IdName.CategoryEnum.Faction,
        IdName.CategoryEnum.InventoryType,
        IdName.CategoryEnum.Group,
        IdName.CategoryEnum.SolarSystem,
        IdName.CategoryEnum.Region,
    ];

    /// <summary>本地类别是否可用于 ZKB 实体查询。</summary>
    public static bool IsSupported(IdName.CategoryEnum category) => TryToEntityType(category, out _);

    /// <summary>本地 <see cref="IdName.CategoryEnum"/> → ZKB 实体类型。</summary>
    public static bool TryToEntityType(IdName.CategoryEnum category, out EntityType entityType)
    {
        switch (category)
        {
            case IdName.CategoryEnum.Character:
                entityType = EntityType.CharacterID;
                return true;
            case IdName.CategoryEnum.Corporation:
                entityType = EntityType.CorporationID;
                return true;
            case IdName.CategoryEnum.Alliance:
                entityType = EntityType.AllianceID;
                return true;
            case IdName.CategoryEnum.Faction:
                entityType = EntityType.FactionID;
                return true;
            case IdName.CategoryEnum.InventoryType:
                entityType = EntityType.ShipTypeID;
                return true;
            case IdName.CategoryEnum.Group:
                entityType = EntityType.GroupID;
                return true;
            case IdName.CategoryEnum.SolarSystem:
                entityType = EntityType.SolarSystemID;
                return true;
            case IdName.CategoryEnum.Region:
                entityType = EntityType.RegionID;
                return true;
            default:
                entityType = default;
                return false;
        }
    }

    /// <summary>ZKB 实体类型 → 本地类别。</summary>
    public static IdName.CategoryEnum ToCategory(EntityType entityType) => entityType switch
    {
        EntityType.CorporationID => IdName.CategoryEnum.Corporation,
        EntityType.AllianceID => IdName.CategoryEnum.Alliance,
        EntityType.FactionID => IdName.CategoryEnum.Faction,
        EntityType.ShipTypeID => IdName.CategoryEnum.InventoryType,
        EntityType.GroupID => IdName.CategoryEnum.Group,
        EntityType.SolarSystemID => IdName.CategoryEnum.SolarSystem,
        EntityType.RegionID => IdName.CategoryEnum.Region,
        _ => IdName.CategoryEnum.Character,
    };

    /// <summary>ZKB 统计接口返回的类型（<see cref="EntityStatistic.StatisticType"/>）→ ZKB 实体类型。</summary>
    public static EntityType ToEntityType(EntityStatisticType type) => type switch
    {
        EntityStatisticType.Corporation => EntityType.CorporationID,
        EntityStatisticType.Alliance => EntityType.AllianceID,
        EntityStatisticType.Faction => EntityType.FactionID,
        EntityStatisticType.ShipType => EntityType.ShipTypeID,
        EntityStatisticType.Group => EntityType.GroupID,
        EntityStatisticType.SolarSystem => EntityType.SolarSystemID,
        EntityStatisticType.Region => EntityType.RegionID,
        _ => EntityType.CharacterID,
    };

    /// <summary>ZKB 统计接口返回的类型（<see cref="EntityStatistic.StatisticType"/>）→ 本地类别。</summary>
    public static IdName.CategoryEnum ToCategory(EntityStatisticType type) => type switch
    {
        EntityStatisticType.Corporation => IdName.CategoryEnum.Corporation,
        EntityStatisticType.Alliance => IdName.CategoryEnum.Alliance,
        EntityStatisticType.Faction => IdName.CategoryEnum.Faction,
        EntityStatisticType.ShipType => IdName.CategoryEnum.InventoryType,
        EntityStatisticType.Group => IdName.CategoryEnum.Group,
        EntityStatisticType.SolarSystem => IdName.CategoryEnum.SolarSystem,
        EntityStatisticType.Region => IdName.CategoryEnum.Region,
        _ => IdName.CategoryEnum.Character,
    };

    /// <summary>ZKB 实体类型 → 列表查询（kills 接口）使用的 <see cref="ParamModifier"/>。</summary>
    public static ParamModifier ToParamModifier(EntityType entityType) => entityType switch
    {
        EntityType.CorporationID => ParamModifier.CorporationID,
        EntityType.AllianceID => ParamModifier.AllianceID,
        EntityType.FactionID => ParamModifier.FactionID,
        EntityType.ShipTypeID => ParamModifier.ShipTypeID,
        EntityType.GroupID => ParamModifier.GroupID,
        EntityType.SolarSystemID => ParamModifier.SystemID,
        EntityType.RegionID => ParamModifier.RegionID,
        _ => ParamModifier.CharacterID,
    };

    /// <summary>本地类别 → 语言键后缀（键前缀 <c>CategoryEnum_</c> 在两种语言文件里均已定义）。</summary>
    public static string ToCategoryLocalizationKey(IdName.CategoryEnum category) => category switch
    {
        IdName.CategoryEnum.Alliance => "CategoryEnum_Alliance",
        IdName.CategoryEnum.Character => "CategoryEnum_Character",
        IdName.CategoryEnum.Constellation => "CategoryEnum_Constellation",
        IdName.CategoryEnum.Corporation => "CategoryEnum_Corporation",
        IdName.CategoryEnum.InventoryType => "CategoryEnum_InventoryType",
        IdName.CategoryEnum.Region => "CategoryEnum_Region",
        IdName.CategoryEnum.SolarSystem => "CategoryEnum_SolarSystem",
        IdName.CategoryEnum.Station => "CategoryEnum_Station",
        IdName.CategoryEnum.Faction => "CategoryEnum_Faction",
        IdName.CategoryEnum.Structure => "CategoryEnum_Structure",
        IdName.CategoryEnum.Group => "CategoryEnum_Group",
        _ => "CategoryEnum_InventoryType",
    };

    /// <summary>ZKB 统计类型 → 实体页标题的语言键（键值沿用 WinUI 原文）。</summary>
    public static string ToEntityTitleLocalizationKey(EntityStatisticType type) => type switch
    {
        EntityStatisticType.Corporation => "EntityStatistPage_Corporation",
        EntityStatisticType.Alliance => "EntityStatistPage_Alliance",
        EntityStatisticType.Faction => "EntityStatistPage_Faction",
        EntityStatisticType.ShipType => "EntityStatistPage_Ship",
        EntityStatisticType.Group => "EntityStatistPage_Class",
        EntityStatisticType.SolarSystem => "EntityStatistPage_System",
        EntityStatisticType.Region => "EntityStatistPage_Region",
        _ => "EntityStatistPage_Character",
    };

    /// <summary>zkillboard.com 上单个 killmail 的网页地址。</summary>
    public static string BuildKillWebUrl(long killId) => $"https://zkillboard.com/kill/{killId}/";

    /// <summary>zkillboard.com 上实体的网页地址（段名按 zkillboard 实际路由，不使用枚举名去 "ID" 的写法）。</summary>
    public static string BuildEntityWebUrl(EntityType entityType, int id)
    {
        var segment = entityType switch
        {
            EntityType.CorporationID => "corporation",
            EntityType.AllianceID => "alliance",
            EntityType.FactionID => "faction",
            EntityType.ShipTypeID => "ship",
            EntityType.GroupID => "group",
            EntityType.SolarSystemID => "system",
            EntityType.RegionID => "region",
            _ => "character",
        };
        return $"https://zkillboard.com/{segment}/{id}/";
    }
}
