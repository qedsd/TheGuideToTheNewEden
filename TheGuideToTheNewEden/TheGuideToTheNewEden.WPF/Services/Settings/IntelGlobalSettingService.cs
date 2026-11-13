using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>
/// 频道预警小窗的全局外观设置持久化（阶段 307）：
/// <c>%LocalAppData%\TheGuideToTheNewEden\Configs\IntelGlobalSetting.json</c>。
/// 对齐 <see cref="IntelSettingService"/> 的读写方式；改值即写盘。
/// </summary>
public static class IntelGlobalSettingService
{
    private static readonly string FilePath = System.IO.Path.Combine(SettingsService.DataPath, "Configs", "IntelGlobalSetting.json");

    private static IntelGlobalSetting? _value;

    public static IntelGlobalSetting Value
    {
        get
        {
            if (_value is null)
            {
                Load();
                _value ??= new IntelGlobalSetting();
                _value.PropertyChanged += (_, _) => Save();
            }

            return _value;
        }
    }

    /// <summary>阈值等设置变化后由 <see cref="IntelMapView"/> 订阅刷新星图标签。</summary>
    public static event EventHandler? SettingChanged;

    private static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                _value = JsonConvert.DeserializeObject<IntelGlobalSetting>(File.ReadAllText(FilePath));
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private static void Save()
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(_value, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    /// <summary>由 <see cref="IntelMapView"/> 在初始化时订阅；任何全局设置变化都触发星图刷新。</summary>
    internal static void RaiseSettingChanged() => SettingChanged?.Invoke(null, EventArgs.Empty);
}
