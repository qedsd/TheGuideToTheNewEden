using System.IO;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services;

/// <summary>
/// Core 初始化：设置 Core.Config 的路径与行为开关，并打开数据库。
/// 对应 WinUI 项目的 ActivationService，必须在任何 Core 业务调用之前执行。
/// </summary>
public static class CoreInitializer
{
    public static bool DatabaseReady { get; private set; }

    public static void Init()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        SettingsService.Initialize();

        // 各设置子服务先加载，以便把值同步给 Core.Config。
        GameServerSelectorService.Initialize();
        LocalDbSelectorService.Initialize();
        DBLocalizationSettingService.Initialize();
        PlayerStatusService.Initialize();
        AutoUpdateService.Initialize();
        GameLogsSettingService.Initialize();

        Config.DBPath = Path.Combine(baseDir, "Resources", "Database", "main.db");
        Config.StaticDBPath = Path.Combine(baseDir, "Resources", "Database", "static.db");
        Config.DEDDBPath = Path.Combine(baseDir, "Resources", "Database", "ded.db");
        Config.CacheDBPath = Path.Combine(SettingsService.DataPath, "Configs", "cache.db");
        Config.RegionMapPath = Path.Combine(baseDir, "Resources", "Configs", "RegionMap.json");
        Config.CapitalJumpShipInfoPath = Path.Combine(baseDir, "Resources", "Configs", "CapitalJumpShipInfo.json");
        Config.AppDataPath = SettingsService.DataPath;

        Config.NeedLocalization = DBLocalizationSettingService.Value;
        Config.LocalDBPath = LocalDbSelectorService.Value;
        Config.DefaultGameServer = GameServerSelectorService.Value;
        Config.PlayerStatusApi = PlayerStatusService.Value;

        ApplyEsiCredentials();
        CharacterStore.Init();

        Log.Init();
        DatabaseReady = Config.InitDb();

        ZKB.NET.Config.UserAgent = "TheGuideToTheNewEden";

        if (!DatabaseReady)
        {
            Log.Error("数据库初始化失败，部分功能不可用。");
        }
    }

    /// <summary>游戏服务器已切换（凭据、ESI 单例与角色列表都已换到新服务器）。</summary>
    public static event EventHandler? GameServerChanged;

    /// <summary>
    /// 运行时切换游戏服务器：写设置 → 重灌 ESI 凭据 → 重建 ESI 单例 → 载入该国服的已授权角色。
    /// </summary>
    /// <remarks>
    /// 不做这一步的话，切服后仍沿用旧服务器的客户端凭据与数据源（国服会直接授权失败），
    /// 且角色列表还是另一个服务器的。切换完成后会触发 <see cref="GameServerChanged"/>，
    /// 由界面负责刷新（角色页据此清掉另一服务器的标签并重载卡片）。
    /// </remarks>
    public static void SwitchGameServer(GameServerType server)
    {
        if (GameServerSelectorService.Value == server)
        {
            return;
        }

        GameServerSelectorService.Set(server);
        ApplyEsiCredentials();
        Core.Services.ESIService.Reset();
        CharacterStore.Init();
        GameServerChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// 灌入 ESI 授权所需配置：客户端凭据来自 Configs/ESILicense.txt，权限范围来自 ESI 权限设置。
    /// 必须在首次访问 Core.Services.ESIService.Current 之前完成（SSO 实例在构造时读取 Config）。
    /// </summary>
    public static void ApplyEsiCredentials()
    {
        var licenseFile = Path.Combine(SettingsService.DataPath, "Configs", "ESILicense.txt");
        if (File.Exists(licenseFile))
        {
            try
            {
                var lines = File.ReadAllLines(licenseFile);
                if (lines.Length >= 3)
                {
                    Config.ClientId = GameServerSelectorService.Value == GameServerType.Serenity
                        ? SerenityAuthHelper.ClientId
                        : lines[0].Trim();
                    Config.ESICallback = lines[1].Trim();
                    Config.ClientSecret = lines[2].Trim();
                }
                else
                {
                    Log.Error("ESILicense.txt 内容不足 3 行，无法读取 ESI 凭据");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }
        else
        {
            Log.Error("未找到 Configs/ESILicense.txt，无法进行新的 ESI 授权");
        }

        Config.Scopes = ESIScopeService.Current.GetSelectedScopes().ToList();
    }
}