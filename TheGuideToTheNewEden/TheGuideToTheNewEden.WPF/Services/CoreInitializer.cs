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

    /// <summary>
    /// 灌入 ESI 授权所需配置：客户端凭据来自 Configs/ESILicense.txt，权限范围来自 ESI 权限设置。
    /// 必须在首次访问 Core.Services.ESIService.Current 之前完成（SSO 实例在构造时读取 Config）。
    /// </summary>
    private static void ApplyEsiCredentials()
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