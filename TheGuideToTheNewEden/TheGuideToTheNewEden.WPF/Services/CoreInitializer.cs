using System.IO;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Services.Translation;

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

        // 日志必须**最先**装起来：下面任何一步（设置读取、ESI 凭据检查、角色/结构载入）都可能写日志，
        // 而"首次运行 Configs 不存在"时正是凭据检查那条提示触发了 Log 未初始化 → NRE、界面直接崩
        Log.Init();

        SettingsService.Initialize();

        // 各设置子服务先加载，以便把值同步给 Core.Config。
        GameServerSelectorService.Initialize();
        LocalDbSelectorService.Initialize();
        DBLocalizationSettingService.Initialize();
        // 模型端点先加载（翻译设置的 AiEndpoint / AiStream 等都转发到它）
        AiModelSettingService.Initialize();
        TranslationSettingService.Initialize();
        AiAssistantSettingService.Initialize();
        ChannelTranslationSettingService.Initialize();
        UserGlossaryService.Initialize();
        GlossaryCandidateService.Initialize();
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
        // 结构列表（用户的市场建筑 + 结构名称缓存）必须在任何按 ID 查询之前载入：
        // 否则 GetMarketStrutures() 是空集合，一次保存就会把 MarketStructures.json 覆盖成空。
        StructureService.Init();

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
    /// 灌入 ESI 授权所需配置：客户端凭据优先来自**发布内置常量**（<see cref="EsiLicenseRelease"/>），
    /// 未内置时回退 <c>Configs/ESILicense.txt</c>；权限范围来自 ESI 权限设置。
    /// 必须在首次访问 Core.Services.ESIService.Current 之前完成（SSO 实例在构造时读取 Config）。
    /// </summary>
    public static void ApplyEsiCredentials()
    {
        var lines = ReadEsiLicenseLines(out var source);
        if (lines is not null)
        {
            Config.ClientId = GameServerSelectorService.Value == GameServerType.Serenity
                ? SerenityAuthHelper.ClientId
                : lines[0].Trim();
            Config.ESICallback = lines[1].Trim();
            Config.ClientSecret = lines[2].Trim();
            Log.Info($"ESI 凭据来源：{source}");
        }
        else
        {
            // 没有内置、也没有可用的本地文件：不是崩溃，只是暂时无法发起新的授权。
            Log.Warn("未找到可用的 ESI 凭据（发布内置为空，且 Configs/ESILicense.txt 不存在或不足 3 行），无法进行新的 ESI 授权");
        }

        Config.Scopes = ESIScopeService.Current.GetSelectedScopes().ToList();

        // 凭据或服务器可能已变化：丢弃按旧 Config 构造的 SSO / ESI 客户端单例。
        Core.Services.ESIService.Reset();
    }

    /// <summary>
    /// 读取 ESI 凭据，顺序为 ClientId / Callback / ClientSecret。
    /// <para>
    /// 优先级：**发布内置常量**（<see cref="EsiLicenseRelease"/>，发布版用）→
    /// <c>Configs/ESILicense.txt</c>（开发/运维临时覆盖）→ 无。
    /// 内置值三项都填写后，终端用户无需任何配置文件；本地开发把内置值留空即可继续用文件。
    /// </para>
    /// </summary>
    private static string[]? ReadEsiLicenseLines(out string source)
    {
        if (EsiLicenseRelease.IsConfigured)
        {
            source = "发布内置（EsiLicenseRelease.cs）";
            return EsiLicenseRelease.Lines;
        }

        var licenseFile = Path.Combine(SettingsService.DataPath, "Configs", "ESILicense.txt");
        if (File.Exists(licenseFile))
        {
            try
            {
                var lines = File.ReadAllLines(licenseFile);
                if (IsUsableEsiLicense(lines))
                {
                    source = licenseFile;
                    return lines;
                }

                Log.Warn($"Configs/ESILicense.txt 不足 3 行或有空行：{licenseFile}");
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        source = string.Empty;
        return null;
    }

    /// <summary>三项齐全且都非空才算可用，避免用半个凭据发起授权。</summary>
    private static bool IsUsableEsiLicense(string[]? lines)
        => lines is { Length: >= 3 }
           && !string.IsNullOrWhiteSpace(lines[0])
           && !string.IsNullOrWhiteSpace(lines[1])
           && !string.IsNullOrWhiteSpace(lines[2]);
}