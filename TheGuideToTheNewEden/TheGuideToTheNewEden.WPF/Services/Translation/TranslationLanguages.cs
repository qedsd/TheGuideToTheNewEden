namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>
/// 翻译语言代码表。方向不再写死成"中英互译"，而是 <b>源语言 → 目标语言</b> 两个代码：
/// 源语言可用 <see cref="Auto"/>（按正文脚本自动判定），目标语言必须是具体语言。
/// <para>
/// 代码沿用 Core 的取值（Core 里中文是 <c>zh-CHS</c>、英文是 <c>en</c>），凡是 <c>zh</c> 开头都归一成
/// <see cref="Chinese"/>，所以两边可以直接互转。AI 源支持全部语言；本地词库源只支持中英（SDE 只有中英对照）。
/// </para>
/// </summary>
public static class TranslationLanguages
{
    /// <summary>源语言"自动判定"。</summary>
    public const string Auto = "auto";

    public const string Chinese = "zh";

    public const string English = "en";

    public const string Japanese = "ja";

    public const string Korean = "ko";

    public const string Russian = "ru";

    public const string German = "de";

    public const string French = "fr";

    public const string Spanish = "es";

    /// <summary>可作为目标语言的语言（下拉顺序）。</summary>
    public static readonly string[] Targets =
    [
        Chinese, English, Japanese, Korean, Russian, German, French, Spanish,
    ];

    /// <summary>
    /// 目标语言下拉的选项：**第一项是「自动检测」（目标位置上的 auto，语义是"译成另一种语言"）**——原文是中文就译成英文、是英文就译成中文，
    /// 其它语言（日/韩/俄…）默认译成中文。这样"发中文还是英文"都不用改设置。
    /// </summary>
    public static readonly string[] TargetOptions =
    [
        Auto, .. Targets,
    ];

    /// <summary>可作为源语言的语言（含"自动"）。</summary>
    public static readonly string[] Sources =
    [
        Auto, .. Targets,
    ];

    /// <summary>把外部语言代码归一化成这里的取值（Core 的 <c>zh-CHS</c> → <c>zh</c>）。</summary>
    public static string Normalize(string? code)
    {
        var value = (code ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return Auto;
        }

        if (value.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return Chinese;
        }

        if (value.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            return Japanese;
        }

        if (value.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
        {
            return Korean;
        }

        if (value.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
        {
            return Russian;
        }

        if (value.StartsWith("de", StringComparison.OrdinalIgnoreCase))
        {
            return German;
        }

        if (value.StartsWith("fr", StringComparison.OrdinalIgnoreCase))
        {
            return French;
        }

        if (value.StartsWith("es", StringComparison.OrdinalIgnoreCase))
        {
            return Spanish;
        }

        if (value.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return English;
        }

        return value;
    }

    /// <summary>是否是认识的取值（源语言含 auto）。</summary>
    public static bool IsSource(string? code) => Array.IndexOf(Sources, Normalize(code)) >= 0;

    /// <summary>是否是认识的目标语言（不含 auto；auto 用 <see cref="IsTargetOption"/> 判断）。</summary>
    public static bool IsTarget(string? code) => Array.IndexOf(Targets, Normalize(code)) >= 0;

    /// <summary>是否是目标语言下拉里的合法选项（含「自动检测」（目标位置上的 auto，语义是"译成另一种语言"））。</summary>
    public static bool IsTargetOption(string? code)
        => Normalize(code) == Auto || IsTarget(code);

    /// <summary>中文 ↔ 英文的组合（术语库与本地词库只覆盖这一对）。</summary>
    public static bool IsChineseEnglishPair(string? from, string? to)
    {
        var f = Normalize(from);
        var t = Normalize(to);
        return (f == Chinese && t == English) || (f == English && t == Chinese);
    }

    /// <summary>语言代码 → 本地化键。</summary>
    public static string DisplayKey(string? code) => $"TranslationPage_Language_{Normalize(code)}";

    /// <summary>目标语言代码 → 本地化键（<c>auto</c> 在目标位置上是「自动检测」（目标位置上的 auto，语义是"译成另一种语言"），说法与源语言不同）。</summary>
    public static string TargetDisplayKey(string? code)
        => Normalize(code) == Auto ? "TranslationPage_Language_AutoTarget" : DisplayKey(code);
}
