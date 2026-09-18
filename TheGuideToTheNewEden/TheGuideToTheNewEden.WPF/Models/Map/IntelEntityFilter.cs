using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.WPF.Models.Map;

/// <summary>
/// 情报实体过滤器——**语义与 WinUI 的 <c>MapIntelFilter</c> 完全一致**（同一份 <c>MapIntelConfig.Exclusions/Inclusions</c> 配置）：
/// 六类实体各一个集合（角色 / 军团 / 联盟 / 星系 / 星域 / 物品类型），
/// <see cref="EmptyAlwayContains"/> 决定"某类集合为空"时的默认答案：
/// <list type="bullet">
/// <item>排除项用 <c>false</c>：该类为空 → <see cref="Contains"/> 恒 false → 不拦任何东西；</item>
/// <item>包含项用 <c>true</c>：该类为空 → 恒 true → 放行一切（即"包含项非空时只保留命中的"）。</item>
/// </list>
/// </summary>
public sealed class IntelEntityFilter
{
    public HashSet<int> Characters { get; } = [];

    public HashSet<int> Corporations { get; } = [];

    public HashSet<int> Alliances { get; } = [];

    public HashSet<int> SolarSystems { get; } = [];

    public HashSet<int> Regions { get; } = [];

    public HashSet<int> Types { get; } = [];

    /// <summary>集合为空时的默认答案：排除项 false / 包含项 true。</summary>
    public bool EmptyAlwayContains { get; init; }

    public void Clear()
    {
        Characters.Clear();
        Corporations.Clear();
        Alliances.Clear();
        SolarSystems.Clear();
        Regions.Clear();
        Types.Clear();
    }

    /// <summary>把实体列表装进对应类别的集合（未知类别忽略——旧版 WPF 把它当关键词用的条目正好落在这里被忽略）。</summary>
    public void Add(IEnumerable<IdName>? idNames)
    {
        if (idNames is null)
        {
            return;
        }

        foreach (var idName in idNames)
        {
            switch (idName.GetCategory())
            {
                case IdName.CategoryEnum.Character:
                    Characters.Add(idName.Id);
                    break;
                case IdName.CategoryEnum.Corporation:
                    Corporations.Add(idName.Id);
                    break;
                case IdName.CategoryEnum.Alliance:
                    Alliances.Add(idName.Id);
                    break;
                case IdName.CategoryEnum.SolarSystem:
                    SolarSystems.Add(idName.Id);
                    break;
                case IdName.CategoryEnum.Region:
                    Regions.Add(idName.Id);
                    break;
                case IdName.CategoryEnum.InventoryType:
                    Types.Add(idName.Id);
                    break;
            }
        }
    }

    public bool Contains(IdName.CategoryEnum category, int id) => category switch
    {
        IdName.CategoryEnum.Character => Characters.Count == 0 ? EmptyAlwayContains : Characters.Contains(id),
        IdName.CategoryEnum.Corporation => Corporations.Count == 0 ? EmptyAlwayContains : Corporations.Contains(id),
        IdName.CategoryEnum.Alliance => Alliances.Count == 0 ? EmptyAlwayContains : Alliances.Contains(id),
        IdName.CategoryEnum.SolarSystem => SolarSystems.Count == 0 ? EmptyAlwayContains : SolarSystems.Contains(id),
        IdName.CategoryEnum.Region => Regions.Count == 0 ? EmptyAlwayContains : Regions.Contains(id),
        IdName.CategoryEnum.InventoryType => Types.Count == 0 ? EmptyAlwayContains : Types.Contains(id),
        _ => false,
    };

    /// <summary>给定类别的一组 id 里"只要有一个命中"就返回 true（与 WinUI 的 any-of 一致）。</summary>
    public bool Contains(IdName.CategoryEnum category, IEnumerable<int> ids)
    {
        foreach (var id in ids)
        {
            if (Contains(category, id))
            {
                return true;
            }
        }

        return false;
    }
}
