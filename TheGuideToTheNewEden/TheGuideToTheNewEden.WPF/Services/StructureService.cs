using System.Collections.ObjectModel;
using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Models.Universe;

namespace TheGuideToTheNewEden.WPF.Services;

/// <summary>
/// 市场结构管理：读写用户添加的结构列表。
/// 说明：通过 ESI 按 ID/按角色查询结构依赖账号授权，授权流程尚未移植，
/// 因此这里仅提供本地列表的增删查与持久化。
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

    /// <summary>按 ID 添加一个结构到市场结构列表（已存在则忽略）。返回值表示是否新增。</summary>
    public static bool Add(long id, string? name)
    {
        var structures = GetMarketStrutures();
        if (structures.Any(p => p.Id == id))
        {
            return false;
        }

        structures.Add(new Structure { Id = id, Name = name ?? id.ToString() });
        SaveMarketStrutures();
        return true;
    }

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