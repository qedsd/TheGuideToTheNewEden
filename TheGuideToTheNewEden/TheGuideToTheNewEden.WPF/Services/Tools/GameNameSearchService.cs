using System.Threading;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.WPF.Services.Tools;

/// <summary>
/// 游戏名称的合并搜索（移植自 WinUI 版 <c>IMEPage.Serach</c>）：一次查遍
/// 角色/军团/联盟（IdName，含 ESI 兜底）+ 物品 + 物品分组 + 星系 + 星域，按 Id 去重、按名称长度升序。
/// <para>
/// 与 WinUI 版的差异：① 收口到服务层（原版逻辑写在页面代码后置里），异常统一记日志并返回空表；
/// ② 支持 <see cref="CancellationToken"/>（界面每次输入都会取消上一轮）；
/// ③ **不再吞掉"某个库没载入"**——Core 的各个 Search 在没有数据库时会抛异常，
/// 这里逐类 try/catch，坏掉一类只少一类结果，不影响其余（§9 第 57 条的同款纪律）。
/// </para>
/// </summary>
public static class GameNameSearchService
{
    /// <summary>建议条数上限（WinUI 也是 6）。</summary>
    public const int SuggestionLimit = 6;

    /// <summary>结果列表上限（WinUI 也是 100）。</summary>
    public const int ResultLimit = 100;

    /// <summary>搜索名称。失败/无数据库时返回空表。</summary>
    public static List<IdName> Search(string? keyword, CancellationToken cancellationToken = default)
    {
        var result = new List<IdName>();
        var text = keyword?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return result;
        }

        try
        {
            // 1) 角色 / 军团 / 联盟 / 空间站等（本地缓存 + ESI）
            AppendIdNames(result, SafeSearch(() => Core.Services.IDNameService.SerachByName(text), "IDName"));

            // 2) 物品 / 分组 / 星系 / 星域（本地 SDE）
            AppendSearchItems(result, SafeSearch(() => Core.Services.DB.InvTypeService.Search(text), "InvType"),
                IdName.CategoryEnum.InventoryType);
            AppendSearchItems(result, SafeSearch(() => Core.Services.DB.InvGroupService.Search(text), "InvGroup"),
                IdName.CategoryEnum.Group);
            AppendSearchItems(result, SafeSearch(() => Core.Services.DB.MapSolarSystemService.Search(text), "MapSolarSystem"),
                IdName.CategoryEnum.SolarSystem);
            AppendSearchItems(result, SafeSearch(() => Core.Services.DB.MapRegionService.Search(text), "MapRegion"),
                IdName.CategoryEnum.Region);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new List<IdName>();
        }

        return result
            .Where(p => !string.IsNullOrEmpty(p.Name))
            .OrderBy(p => p.Name.Length)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>取建议列表（去重之后的前 <see cref="SuggestionLimit"/> 条）。</summary>
    public static List<IdName> SearchSuggestions(string? keyword, CancellationToken cancellationToken = default)
        => Search(keyword, cancellationToken).Take(SuggestionLimit).ToList();

    private static List<T> SafeSearch<T>(Func<List<T>> search, string kind)
    {
        try
        {
            return search() ?? new List<T>();
        }
        catch (Exception ex)
        {
            // 例如对应数据库没载入：只少这一类结果，其余照常。
            Core.Log.Error($"[GameNameSearch] {kind} 搜索失败：{ex.Message}");
            return new List<T>();
        }
    }

    private static void AppendIdNames(List<IdName> target, List<IdName> source)
    {
        var ids = target.Select(p => p.Id).ToHashSet();
        foreach (var item in source)
        {
            if (ids.Add(item.Id))
            {
                target.Add(item);
            }
        }
    }

    private static void AppendSearchItems(List<IdName> target, List<Core.Models.DataBaseSearchItem> source,
        IdName.CategoryEnum category)
    {
        var expected = ToDataBaseItemType(category);
        if (expected is null)
        {
            return;
        }

        var ids = target.Select(p => p.Id).ToHashSet();
        foreach (var item in source)
        {
            if (item is null || item.ID <= 0 || ids.Contains(item.ID))
            {
                continue;
            }

            if (item.Type != expected)
            {
                // Core 的 Search 已经按具体表返回，这里按类型再确认一次，避免张冠李戴。
                continue;
            }

            ids.Add(item.ID);
            target.Add(new IdName
            {
                Id = item.ID,
                Name = item.Name,
                Category = (int)category,
            });
        }
    }

    /// <summary>类别 → Core 的数据库项类型（<see cref="DataBaseItemType"/> 里没有"未知"，不认识就返回 null 跳过）。</summary>
    private static DataBaseItemType? ToDataBaseItemType(IdName.CategoryEnum category)
        => category switch
        {
            IdName.CategoryEnum.InventoryType => DataBaseItemType.InvType,
            IdName.CategoryEnum.Group => DataBaseItemType.InvGroup,
            IdName.CategoryEnum.SolarSystem => DataBaseItemType.MapSolarSystem,
            IdName.CategoryEnum.Region => DataBaseItemType.MapRegion,
            _ => null,
        };
}
