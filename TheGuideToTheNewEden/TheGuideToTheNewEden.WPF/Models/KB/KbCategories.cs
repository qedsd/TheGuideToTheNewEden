using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.WPF.Models.KB;

/// <summary>ZKB 过滤器可选实体类别（供 XAML 的 <c>{x:Static}</c> 引用）。</summary>
public static class KbCategories
{
    /// <summary>通用过滤（作用在 KB 本身的星系 / 星域 / 被击杀舰船类型）。</summary>
    public static readonly IdName.CategoryEnum[] Common =
    [
        IdName.CategoryEnum.SolarSystem,
        IdName.CategoryEnum.Region,
        IdName.CategoryEnum.InventoryType,
    ];

    /// <summary>角色相关过滤（受害者 / 攻击者的角色、军团、联盟）。</summary>
    public static readonly IdName.CategoryEnum[] Role =
    [
        IdName.CategoryEnum.Character,
        IdName.CategoryEnum.Corporation,
        IdName.CategoryEnum.Alliance,
    ];
}
