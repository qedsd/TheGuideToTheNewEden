using System.IO;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services.Tools;
using TheGuideToTheNewEden.WPF.Views.Pages;
using TheGuideToTheNewEden.WPF.Views.Pages.Settings;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Services.Home;

/// <summary>
/// 打开一个功能（主页磁贴与导航右键菜单共用同一实现）。
/// <para>
/// 三类功能的处理方式刻意不同：功能页走 <see cref="Navigation"/>；小工具走
/// <see cref="ToolWindowHost"/>（<b>不要</b>用 Navigate——那会先实例化页面、再被
/// MainWindow 的 <c>Navigating</c> 拦下来开窗，绕一圈）；动作项直接执行。
/// </para>
/// <para>
/// <b>带参数的磁贴</b>（P3）：<c>market + 物品 ID</c> → 市场页选中该物品；
/// <c>characters + 角色 ID</c> → 打开该角色的工作区标签；<c>map + 星系 ID</c> → 星图定位到该星系。
/// 参数走 <see cref="Navigation"/> 里既有的"待处理请求"通道，因此页面是否已经创建都能正确落地。
/// </para>
/// </summary>
public static class HomeFeatureLauncher
{
    public static void Open(HomeFeature? feature, string? param = null)
    {
        if (feature is null)
        {
            return;
        }

        // 带参数的磁贴优先按"跳转 + 选中/定位"处理。
        if (!string.IsNullOrWhiteSpace(param) && TryOpenWithParam(feature.Id, param!))
        {
            return;
        }

        switch (feature.Kind)
        {
            case HomeFeatureKind.Page:
                if (feature.PageType is not null)
                {
                    Navigation.Navigate(feature.PageType);
                }

                break;

            case HomeFeatureKind.ToolWindow:
                if (feature.PageType is not null)
                {
                    ToolWindowHost.Show(feature.PageType, feature.ToolTitleKey ?? feature.TitleKey);
                }

                break;

            case HomeFeatureKind.Action:
                RunAction(feature.Id);
                break;
        }
    }

    /// <summary>带参数的跳转；参数无效（解析不出来）时返回 false，调用方退回"打开功能本体"。</summary>
    private static bool TryOpenWithParam(string featureId, string param)
    {
        switch (featureId)
        {
            case "market" when long.TryParse(param, out var typeId) && typeId > 0:
                Navigation.NavigateToMarket(typeId);
                return true;

            case "characters" when long.TryParse(param, out var characterId) && characterId > 0:
                Navigation.NavigateToCharacter(characterId);
                return true;

            case "map" when int.TryParse(param, out var systemId) && systemId > 0:
                Navigation.NavigateToSystem(systemId);
                return true;

            default:
                return false;
        }
    }

    /// <summary>动作项。id 与 <see cref="HomeFeatureCatalog"/> 里的一致。</summary>
    private static void RunAction(string id)
    {
        switch (id)
        {
            case "action-open-log":
            {
                // 优先用 log4net 实际挂着的那个 rolling file（log4net.config 若被改过也不用跟着改代码）；
                // 取不到时退回 %LocalAppData%\TheGuideToTheNewEden\Logs\——2026-10-16 起日志统一在 AppData。
                var logFile = Core.Log.GetLogFile();
                if (string.IsNullOrEmpty(logFile) || !File.Exists(logFile))
                {
                    logFile = Path.Combine(Core.Log.GetLogPath(), DateTime.Now.ToString("yyyyMMdd") + ".txt");
                }

                // 交给 shell 定位（不要自己拉 explorer.exe，见 ShellHelper 的说明）；
                // 今天还没有日志时定位会退化为"打开所在目录"，同样可用。
                ShellHelper.RevealInFolder(logFile);
                break;
            }

            case "action-open-config":
            {
                ShellHelper.OpenFolder(Path.Combine(SettingsService.DataPath, "Configs"));
                break;
            }

            case "action-check-update":
            {
                OpenSettingsCategory(typeof(UpdateSettingPage));
                break;
            }

            case "action-manual":
            {
                OpenManual();
                break;
            }

            case "action-about":
            {
                AboutWindow.ShowWindow();
                break;
            }

            default:
            {
                // 设置子页：setting-xxx → 对应的设置分类页
                if (id.StartsWith("setting-", StringComparison.Ordinal) && ResolveSettingPage(id) is { } pageType)
                {
                    OpenSettingsCategory(pageType);
                }

                break;
            }
        }
    }

    /// <summary>跳转到「设置」并直接打开某个分类（复用 SettingsPage 既有的待打开分类机制）。</summary>
    private static void OpenSettingsCategory(Type pageType)
    {
        SettingsPage.RequestCategory(pageType);
        Navigation.Navigate(typeof(SettingsPage));
    }

    private static Type? ResolveSettingPage(string id) => id switch
    {
        "setting-general" => typeof(GeneralSettingPage),
        "setting-game-log" => typeof(GameLogSettingPage),
        "setting-market" => typeof(MarketSettingPage),
        "setting-structures" => typeof(StructuresSettingPage),
        "setting-esi-scope" => typeof(ESIScopeSettingPage),
        "setting-ai-translation" => typeof(AiTranslationSettingPage),
        "setting-glossary" => typeof(GlossarySettingPage),
        "setting-keyboard" => typeof(KeyboardListPage),
        "setting-test" => typeof(TestSettingPage),
        "setting-update" => typeof(UpdateSettingPage),
        _ => null,
    };

    /// <summary>
    /// 打开使用手册（WinUI 版随程序分发的 PDF，按语言取中文 / 英文）。
    /// 文件不在（旧安装包）时给出明确提示，而不是静默什么都不发生。
    /// </summary>
    private static void OpenManual()
    {
        var language = LanguageService.Value;
        var fileName = string.Equals(language, "zh-CN", StringComparison.OrdinalIgnoreCase)
            ? "Manual_zh.pdf"
            : "Manual_en.pdf";
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "Manuals", fileName);

        if (!File.Exists(path))
        {
            PageNotifyService.Warning(string.Format(Resolve("Home.Action.ManualMissing"), path));
            return;
        }

        ShellHelper.OpenFile(path);
    }

    private static string Resolve(string key)
        => System.Windows.Application.Current?.TryFindResource(key) as string ?? key;
}
