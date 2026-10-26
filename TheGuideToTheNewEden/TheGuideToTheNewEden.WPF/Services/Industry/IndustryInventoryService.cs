using System.IO;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.WPF.Services.Industry;

/// <summary>一个库存仓库：物品 TypeID → 数量。</summary>
public sealed class IndustryWarehouse
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = LanguageService.Get("IndustryInventory_NewWarehouse");

    public DateTime Updated { get; set; } = DateTime.Now;

    public Dictionary<int, long> Items { get; set; } = new();
}

/// <summary>
/// 工业库存仓库的持久化：<c>Configs/IndustryInventory.json</c>（所有仓库一个文件）。
/// 任何修改后调用 <see cref="Save"/>，会持久化并触发 <see cref="Changed"/>。
/// </summary>
public static class IndustryInventoryService
{
    private static readonly string FilePath = Path.Combine(SettingsService.DataPath, "Configs", "IndustryInventory.json");

    private static List<IndustryWarehouse>? _warehouses;

    /// <summary>仓库数据变化（新建/删除/改名/改数量/导入）后触发。</summary>
    public static event EventHandler? Changed;

    public static IReadOnlyList<IndustryWarehouse> Warehouses
    {
        get
        {
            EnsureLoaded();
            return _warehouses!;
        }
    }

    public static IndustryWarehouse? Find(string? id)
        => string.IsNullOrEmpty(id) ? null : Warehouses.FirstOrDefault(p => p.Id == id);

    public static IndustryWarehouse Create(string? name)
    {
        EnsureLoaded();
        var warehouse = new IndustryWarehouse
        {
            Name = string.IsNullOrWhiteSpace(name)
                ? LanguageService.Get("IndustryInventory_NewWarehouse")
                : name.Trim(),
        };
        _warehouses!.Add(warehouse);
        Save();
        return warehouse;
    }

    public static void Delete(IndustryWarehouse? warehouse)
    {
        if (warehouse is null) return;
        EnsureLoaded();
        _warehouses!.Remove(warehouse);
        Save();
    }

    public static void Rename(IndustryWarehouse? warehouse, string? name)
    {
        if (warehouse is null || string.IsNullOrWhiteSpace(name)) return;
        EnsureLoaded();
        warehouse.Name = name.Trim();
        warehouse.Updated = DateTime.Now;
        Save();
    }

    public static void Save()
    {
        EnsureLoaded();
        try
        {
            var folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(_warehouses));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static void Reload()
    {
        _warehouses = null;
        EnsureLoaded();
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static void EnsureLoaded()
    {
        if (_warehouses is not null) return;
        try
        {
            _warehouses = File.Exists(FilePath)
                ? JsonConvert.DeserializeObject<List<IndustryWarehouse>>(File.ReadAllText(FilePath)) ?? new List<IndustryWarehouse>()
                : new List<IndustryWarehouse>();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            _warehouses = new List<IndustryWarehouse>();
        }

        foreach (var warehouse in _warehouses)
        {
            if (string.IsNullOrEmpty(warehouse.Id))
            {
                warehouse.Id = Guid.NewGuid().ToString("N");
            }
            warehouse.Items ??= new Dictionary<int, long>();
        }
    }
}
