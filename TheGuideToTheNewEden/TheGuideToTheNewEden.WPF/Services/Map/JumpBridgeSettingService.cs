using System.IO;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.WPF.Services.Map;

/// <summary>一组跳桥（两个相邻星系）。</summary>
public sealed class JumpBridge
{
    public int System1 { get; set; }
    public int System2 { get; set; }

    public JumpBridge()
    {
    }

    public JumpBridge(int system1, int system2)
    {
        System1 = system1;
        System2 = system2;
    }
}

/// <summary>跳桥配置（<c>Configs/JumpBridgeSetting.json</c>）。</summary>
public sealed class JumpBridgeConfig
{
    /// <summary>是否在星图上绘制跳桥。</summary>
    public bool ShowInMap { get; set; } = true;

    public List<JumpBridge> JumpBridges { get; set; } = [];
}

/// <summary>
/// 跳桥设置：<c>%LocalAppData%\TheGuideToTheNewEden\Configs\JumpBridgeSetting.json</c>。
/// 与 WinUI 版同路径同格式（<see cref="JumpBridgeConfig"/>），配置互通。
/// <para>
/// <see cref="GetBridgesDict"/> 返回的是**双向**字典（System1→System2 与 System2→System1 都写入），
/// 可直接作为 <c>ShortestPathHelper.CalStargatePath(start, end, avoid, bridge)</c> 的 bridge 参数。
/// </para>
/// </summary>
public static class JumpBridgeSettingService
{
    private static readonly string FilePath = Path.Combine(SettingsService.DataPath, "Configs", "JumpBridgeSetting.json");
    private static readonly object Locker = new();

    private static JumpBridgeConfig? _value;
    private static Dictionary<int, int>? _dict;

    /// <summary>配置发生变化（新增/删除/显示开关）。</summary>
    public static event EventHandler? SettingChanged;

    public static JumpBridgeConfig Value
    {
        get
        {
            lock (Locker)
            {
                if (_value is null)
                {
                    if (File.Exists(FilePath))
                    {
                        try
                        {
                            _value = JsonConvert.DeserializeObject<JumpBridgeConfig>(File.ReadAllText(FilePath)) ?? new JumpBridgeConfig();
                        }
                        catch (Exception ex)
                        {
                            Core.Log.Error(ex);
                            _value = new JumpBridgeConfig();
                        }
                    }
                    else
                    {
                        _value = new JumpBridgeConfig();
                    }

                    _value.JumpBridges ??= [];
                }

                return _value;
            }
        }
    }

    /// <summary>跳桥列表（引用的是配置内的实例，增删请走 Add / Remove）。</summary>
    public static List<JumpBridge> GetValue() => Value.JumpBridges;

    /// <summary>双向跳桥字典（源星系 → 目标星系），可直接喂给寻路。</summary>
    public static Dictionary<int, int> GetBridgesDict()
    {
        if (_dict is not null)
        {
            return _dict;
        }

        var dict = new Dictionary<int, int>();
        foreach (var bridge in Value.JumpBridges)
        {
            if (bridge.System1 <= 0 || bridge.System2 <= 0 || bridge.System1 == bridge.System2)
            {
                continue;
            }

            dict[bridge.System1] = bridge.System2;
            dict[bridge.System2] = bridge.System1;
        }

        _dict = dict;
        return dict;
    }

    public static bool GetValue(int systemId, out int toSystemId)
        => GetBridgesDict().TryGetValue(systemId, out toSystemId);

    public static bool ExistBridge() => GetBridgesDict().Count > 0;

    public static bool IsShowBridge() => Value.ShowInMap;

    public static void SetShowBridge(bool show)
    {
        if (Value.ShowInMap == show)
        {
            return;
        }

        Value.ShowInMap = show;
        Save();
        SettingChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>新增一组跳桥（任一端已被占用、或两端相同则返回 false，与 WinUI 约束一致）。</summary>
    public static bool Add(int system1, int system2)
    {
        if (system1 <= 0 || system2 <= 0 || system1 == system2)
        {
            return false;
        }

        var bridges = Value.JumpBridges;
        if (bridges.Any(p => p.System1 == system1 || p.System2 == system1 || p.System1 == system2 || p.System2 == system2))
        {
            return false;
        }

        bridges.Add(new JumpBridge(system1, system2));
        Save();
        SettingChanged?.Invoke(null, EventArgs.Empty);
        return true;
    }

    /// <summary>删除一组跳桥（不区分两端顺序）。</summary>
    public static bool Remove(int system1, int system2)
    {
        var bridges = Value.JumpBridges;
        var removed = bridges.RemoveAll(p =>
            (p.System1 == system1 && p.System2 == system2) || (p.System1 == system2 && p.System2 == system1)) > 0;
        if (removed)
        {
            Save();
            SettingChanged?.Invoke(null, EventArgs.Empty);
        }

        return removed;
    }

    private static void Save()
    {
        lock (Locker)
        {
            _dict = null; // 让下一次 GetBridgesDict() 重建
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
}
