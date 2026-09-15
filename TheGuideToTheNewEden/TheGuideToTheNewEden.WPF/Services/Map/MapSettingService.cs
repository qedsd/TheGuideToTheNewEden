using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.Map;

namespace TheGuideToTheNewEden.WPF.Services.Map;

/// <summary>
/// 星图设置持久化：<c>%LocalAppData%\TheGuideToTheNewEden\Configs\MapSettings.json</c>。
/// 与 WinUI 版同一路径、同一格式（<see cref="MapConfig"/>），情报工具配置直接互相沿用。
/// </summary>
public static class MapSettingService
{
    private static readonly string FilePath = Path.Combine(SettingsService.DataPath, "Configs", "MapSettings.json");

    private static MapConfig? _value;

    /// <summary>星图配置（含情报工具 MapIntelConfig）；首次访问时从磁盘加载。</summary>
    public static MapConfig Value
    {
        get
        {
            if (_value is null)
            {
                if (File.Exists(FilePath))
                {
                    try
                    {
                        _value = JsonConvert.DeserializeObject<MapConfig>(File.ReadAllText(FilePath)) ?? new MapConfig();
                    }
                    catch (Exception ex)
                    {
                        Core.Log.Error(ex);
                        _value = new MapConfig();
                    }
                }
                else
                {
                    _value = new MapConfig();
                }
            }

            return _value;
        }
    }

    /// <summary>情报配置的深拷贝（调用方修改后 SaveIntel 落盘）。</summary>
    public static MapIntelConfig GetIntel() => Value.Intel.DepthClone<MapIntelConfig>() ?? new MapIntelConfig();

    /// <summary>把情报配置写回并落盘。</summary>
    public static void SaveIntel(MapIntelConfig intel)
    {
        Value.Intel = intel;
        Save();
    }

    public static void Save()
    {
        try
        {
            var folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(FilePath, JsonConvert.SerializeObject(Value, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }
}
