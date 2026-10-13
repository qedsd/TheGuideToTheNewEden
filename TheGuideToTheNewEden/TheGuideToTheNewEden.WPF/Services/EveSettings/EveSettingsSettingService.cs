using System.IO;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.WPF.Services.EveSettings;

/// <summary>
/// 「配置同步」模块的设置。
/// <para>
/// <b>刻意单放一个文件</b>（不写进与 WinUI 版共用的 <c>settings.json</c>）：
/// ① 不和 WinUI 版共用文件互相覆盖；② 里面有一个"每服务器记忆上次配置文件"的动态字典，
/// 塞进扁平的键值字典既难看又容易与 WinUI 的键撞车。
/// </para>
/// </summary>
public sealed class EveSettingsSyncSetting
{
    /// <summary>EVE 设置根目录；留空 = 自动探测 <c>%LOCALAPPDATA%\CCP\EVE</c>。</summary>
    public string RootFolder { get; set; } = string.Empty;

    /// <summary>上次选中的服务器目录名。</summary>
    public string LastServerDir { get; set; } = string.Empty;

    /// <summary>服务器目录名 → 上次选中的配置文件（进程内刷新/重启都沿用）。</summary>
    public Dictionary<string, string> LastProfileByServer { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>同步 / 还原前先自动做一次"整配置文件"备份（默认开：写游戏设置属于用户数据写操作）。</summary>
    public bool AutoBackupBeforeSync { get; set; } = true;

    /// <summary>检测到 EVE 客户端在运行时先拦截并明确警告（需要用户二次确认才继续）。</summary>
    public bool BlockWhenGameRunning { get; set; } = true;

    /// <summary>游戏进程关键词（逗号分隔）；留空 = 复用「多开」模块的 <c>ProcessKeywords</c>（默认 exefile）。</summary>
    public string ProcessKeywords { get; set; } = string.Empty;

    /// <summary>备份仓库目录；留空 = <c>%LocalAppData%\TheGuideToTheNewEden\EveSettings\Backups</c>。</summary>
    public string BackupRoot { get; set; } = string.Empty;
}

/// <summary>
/// 「配置同步」设置的持久化：<c>Configs/EveSettingsSyncSetting.json</c>。
/// 与其它设置服务同一套路：读取失败即回默认值，不阻塞启动。
/// </summary>
public static class EveSettingsSettingService
{
    private static readonly object Gate = new();

    private static readonly string FilePath =
        Path.Combine(SettingsService.DataPath, "Configs", "EveSettingsSyncSetting.json");

    private static EveSettingsSyncSetting? _value;

    /// <summary>当前设置（首次访问时读取文件）。</summary>
    public static EveSettingsSyncSetting Value
    {
        get
        {
            lock (Gate)
            {
                return _value ??= Load();
            }
        }
    }

    public static EveSettingsSyncSetting Load()
    {
        EveSettingsSyncSetting? setting = null;
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    setting = JsonConvert.DeserializeObject<EveSettingsSyncSetting>(json);
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        setting ??= new EveSettingsSyncSetting();

        // 反序列化出来的字典用的是默认比较器，这里统一成忽略大小写（服务器目录名/配置文件名的比较都按它）。
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in setting.LastProfileByServer)
        {
            normalized[pair.Key] = pair.Value;
        }

        setting.LastProfileByServer = normalized;
        return setting;
    }

    /// <summary>落盘（各 CallSite 改完字段后调用；文件只有几百字节）。</summary>
    public static void Save()
    {
        try
        {
            lock (Gate)
            {
                var folder = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                File.WriteAllText(FilePath, JsonConvert.SerializeObject(Value, Formatting.Indented));
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }
}
