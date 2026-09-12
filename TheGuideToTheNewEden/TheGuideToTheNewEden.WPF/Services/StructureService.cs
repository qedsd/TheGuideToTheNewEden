using System.Collections.ObjectModel;
using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Models.Universe;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services;

/// <summary>
/// 市场结构管理：本地列表读写 + **结构名称的 ESI 解析**。
/// 两个文件互不合并：
/// <list type="bullet">
///   <item><c>Configs/Structures.json</c>：ESI 解析过的结构缓存（自动回写，供订单/合同等复用）；</item>
///   <item><c>Configs/MarketStructures.json</c>：用户在设置页添加的"市场建筑"列表。</item>
/// </list>
/// </summary>
public static class StructureService
{
    private static readonly string MarketStructureFilePath = Path.Combine(
        SettingsService.DataPath, "Configs", "MarketStructures.json");

    private static readonly string AutoStructureFilePath = Path.Combine(
        SettingsService.DataPath, "Configs", "Structures.json");

    private static ObservableCollection<Structure>? _marketStructures;
    private static Dictionary<long, Structure>? _autoStructures;

    public static void Init()
    {
        _autoStructures = ReadList(AutoStructureFilePath)?.ToDictionary(p => p.Id)
            ?? new Dictionary<long, Structure>();

        var list = ReadList(MarketStructureFilePath);
        _marketStructures = list is null
            ? []
            : new ObservableCollection<Structure>(list);
    }

    public static ObservableCollection<Structure> GetMarketStrutures()
    {
        return _marketStructures ??= [];
    }

    public static void SaveMarketStrutures()
    {
        WriteList(MarketStructureFilePath, GetMarketStrutures());
    }

    /// <summary>把一个已解析的结构加入市场建筑列表（已存在则忽略）。返回值表示是否新增。</summary>
    public static bool Add(Structure structure)
    {
        var structures = GetMarketStrutures();
        if (structures.Any(p => p.Id == structure.Id))
        {
            return false;
        }

        structures.Add(structure);
        SaveMarketStrutures();
        return true;
    }

    /// <summary>按 ID 添加一个结构到市场结构列表（已存在则忽略）。返回值表示是否新增。</summary>
    public static bool Add(long id, string? name) =>
        Add(new Structure { Id = id, Name = name ?? id.ToString() });

    public static void Remove(IEnumerable<Structure> structures)
    {
        var list = GetMarketStrutures();
        foreach (var structure in structures.ToList())
        {
            list.Remove(structure);
        }

        SaveMarketStrutures();
    }

    public static Structure? GetStructure(long id)
    {
        if (_autoStructures is not null && _autoStructures.TryGetValue(id, out var auto))
        {
            return auto;
        }

        return GetMarketStrutures().FirstOrDefault(p => p.Id == id);
    }

    public static List<Structure>? GetStructuresOfRegion(long regionId)
    {
        var result = new List<Structure>();

        if (_autoStructures is not null)
        {
            result.AddRange(_autoStructures.Values.Where(p => p.RegionId == regionId));
        }

        foreach (var structure in GetMarketStrutures().Where(p => p.RegionId == regionId))
        {
            if (result.All(p => p.Id != structure.Id))
            {
                result.Add(structure);
            }
        }

        return result.Count == 0 ? null : result;
    }

    // ---------- 结构名称的 ESI 解析 ----------

    /// <summary>
    /// 按结构 ID 解析结构名称（含所在星系/星域）。
    /// 命中本地缓存直接返回；否则用**默认角色**的授权调 ESI 解析并回写 <c>Structures.json</c>。
    /// 注意：结构 ID 约 1e12，绝不能走 <c>IDNameService</c>（int，会静默截断）。
    /// </summary>
    public static async Task<Structure?> QueryStructureAsync(long id)
    {
        var local = GetStructure(id);
        if (local is not null)
        {
            return local;
        }

        var character = await CharacterStore.GetDefaultAsync();
        return character is null ? null : await QueryStructureAsync(id, character.CharacterID);
    }

    /// <summary>
    /// 按结构 ID 解析结构名称，用指定角色的授权调 ESI。
    /// <paramref name="characterId"/> &lt;= 0 时只查本地缓存、不发 ESI（与 WinUI 版 <c>-1</c> 的语义一致）；
    /// 需要 <c>esi-universe.read_structures.v1</c> 权限。
    /// </summary>
    public static async Task<Structure?> QueryStructureAsync(long id, long characterId)
    {
        var local = GetStructure(id);
        if (local is not null)
        {
            return local;
        }

        if (characterId <= 0)
        {
            return null;
        }

        try
        {
            var character = CharacterStore.Get(characterId);
            if (character is null || !await CharacterStore.EnsureTokenValidAsync(character))
            {
                return null;
            }

            var resp = await ESIService.GetDefaultESI().Universe.GetStructureInfoAsync(character.Auth, id);
            if (resp?.Model is null)
            {
                return null;
            }

            var structure = new Structure
            {
                Id = id,
                Name = resp.Model.Name,
                SolarSystemId = (int)resp.Model.SolarSystemId,
                CharacterId = (int)characterId,
            };

            // 星系名与所属星域来自本地 SDE（ESI 只给 SolarSystemId）
            var system = await Core.Services.DB.MapSolarSystemService.QueryAsync(structure.SolarSystemId);
            if (system is not null)
            {
                structure.SolarSystemName = system.SolarSystemName;
                structure.RegionId = system.RegionID;
                var region = await Core.Services.DB.MapRegionService.QueryAsync(system.RegionID);
                structure.RegionName = region?.RegionName;
            }

            _autoStructures ??= new Dictionary<long, Structure>();
            _autoStructures[id] = structure;
            SaveAutoStructures();
            return structure;
        }
        catch (Exception ex)
        {
            // 结构未公开 / 无 market 权限 / 角色无 read_structures 权限时会失败，属预期情况
            Core.Log.Error(ex);
            return null;
        }
    }

    private static void SaveAutoStructures()
    {
        if (_autoStructures is not null)
        {
            WriteList(AutoStructureFilePath, _autoStructures.Values.ToList());
        }
    }

    private static List<Structure>? ReadList(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var list = JsonConvert.DeserializeObject<List<Structure>>(File.ReadAllText(path));
            return list?.Where(p => p is not null).ToList();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    private static void WriteList(string path, IEnumerable<Structure> structures)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(path, JsonConvert.SerializeObject(structures));
    }
}