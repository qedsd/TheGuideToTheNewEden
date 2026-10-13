using System.Text.RegularExpressions;
using TheGuideToTheNewEden.WPF.Models.EveSettings;

namespace TheGuideToTheNewEden.WPF.Services.EveSettings;

/// <summary>
/// 服务器目录名 → 显示名 / 服务器类别 / 角色名解析端点。
/// <para>
/// 关键词表与判定规则移植自 <c>mintnick/eve-settings-manager</c> 的
/// <c>electron/main/ipc/server.ts</c>（<c>SERVER_KEYWORDS</c>），并按实机目录
/// <c>c_ccp_eve_tq_tranquility</c>（现行启动器命名）验证过"包含 tranquil 即 TQ"。
/// </para>
/// <para>
/// 判定一律基于<b>目录名</b>：本应用的 ESI 单例在构造时固化了服务器与凭据
/// （REFACTORING 阶段 14），绝不能用"当前设置的游戏服务器"去解析另一个目录里的角色名。
/// </para>
/// </summary>
public static class EveSettingsServerCatalog
{
    /// <summary>一条关键词规则。</summary>
    public sealed record ServerKeyword(string Keyword, string DisplayName, EveSettingsServerKind Kind);

    private static readonly ServerKeyword[] Table =
    [
        new("tranquil", "Tranquility", EveSettingsServerKind.Tranquility),
        new("serenity", "Serenity", EveSettingsServerKind.Serenity),
        new("infinity", "Infinity", EveSettingsServerKind.Infinity),
        new("singulari", "Singularity", EveSettingsServerKind.Other),
        new("duality", "Duality", EveSettingsServerKind.Other),
        new("thunderdome", "Thunderdome", EveSettingsServerKind.Other),
        // CCP 给锦标赛服务器用的内部目录名
        new("buckshot", "Thunderdome", EveSettingsServerKind.Other),
        new("tornado", "Tornado", EveSettingsServerKind.Other),
    ];

    /// <summary>目录名末尾的语言后缀（如 <c>..._en</c> / <c>...-zh</c>）。</summary>
    private static readonly Regex LanguageSuffix = new("[_-]([a-z]{2})$", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> LanguageNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "English",
        ["de"] = "German",
        ["fr"] = "French",
        ["ru"] = "Russian",
        ["ja"] = "Japanese",
        ["ko"] = "Korean",
        ["zh"] = "Chinese",
        ["es"] = "Spanish",
        ["it"] = "Italian",
        ["pt"] = "Portuguese",
    };

    /// <summary>目录名命中的关键词规则；不命中返回 null（该目录不是服务器目录）。</summary>
    public static ServerKeyword? Match(string? dirName)
    {
        if (string.IsNullOrWhiteSpace(dirName))
        {
            return null;
        }

        var lower = dirName.ToLowerInvariant();
        foreach (var rule in Table)
        {
            if (lower.Contains(rule.Keyword, StringComparison.Ordinal))
            {
                return rule;
            }
        }

        return null;
    }

    /// <summary>该目录名是否像一个服务器目录。</summary>
    public static bool IsServerDirectoryName(string? dirName) => Match(dirName) is not null;

    /// <summary>显示名：关键词表里的名字 + 可选语言后缀（语言名用拉丁写法，两种界面语言下都不必翻译）。</summary>
    public static string BuildDisplayName(string dirName, ServerKeyword rule)
    {
        var match = LanguageSuffix.Match(dirName.ToLowerInvariant());
        if (match.Success && LanguageNames.TryGetValue(match.Groups[1].Value, out var language))
        {
            return $"{rule.DisplayName} ({language})";
        }

        return rule.DisplayName;
    }

    /// <summary>
    /// 角色名解析端点（<c>POST /universe/names/</c>，匿名、无需授权、不含个人信息）。
    /// 奇点/Duality/雷霆穹顶没有公开 ESI，返回 null 表示"该服务器不解析名字"。
    /// </summary>
    public static string? ResolveNamesEndpoint(EveSettingsServerKind kind) => kind switch
    {
        EveSettingsServerKind.Tranquility => "https://esi.evetech.net/universe/names/",
        EveSettingsServerKind.Serenity => "https://ali-esi.evepc.163.com/latest/universe/names/?datasource=serenity",
        EveSettingsServerKind.Infinity => "https://ali-esi.evepc.163.com/latest/universe/names/?datasource=infinity",
        _ => null,
    };

    /// <summary>该服务器是否支持角色名解析。</summary>
    public static bool SupportsNameResolution(EveSettingsServerKind kind)
        => ResolveNamesEndpoint(kind) is not null;
}
