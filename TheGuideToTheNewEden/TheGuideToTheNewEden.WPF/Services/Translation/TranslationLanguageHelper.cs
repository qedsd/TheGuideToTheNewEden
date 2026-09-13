using System.Text.RegularExpressions;

namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>翻译语言判定与显示（方向 = 源语言 → 目标语言，见 <see cref="TranslationLanguages"/>）。</summary>
public static partial class TranslationLanguageHelper
{
    /// <summary>CJK 统一表意文字：出现即视为中文。</summary>
    [GeneratedRegex(@"[\u4e00-\u9fff]")]
    private static partial Regex ChineseRegex();

    /// <summary>日文假名。</summary>
    [GeneratedRegex(@"[\u3040-\u30ff]")]
    private static partial Regex JapaneseRegex();

    /// <summary>韩文谚文。</summary>
    [GeneratedRegex(@"[\uac00-\ud7af\u1100-\u11ff]")]
    private static partial Regex KoreanRegex();

    /// <summary>西里尔字母（俄语等）。</summary>
    [GeneratedRegex(@"[\u0400-\u04ff]")]
    private static partial Regex CyrillicRegex();

    /// <summary>文本里是否含中文。</summary>
    public static bool ContainsChinese(string? text)
        => !string.IsNullOrEmpty(text) && ChineseRegex().IsMatch(text);

    /// <summary>按正文脚本猜源语言（自动方向用）：中文 → 日文 → 韩文 → 俄文 → 默认英文。</summary>
    public static string Detect(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return TranslationLanguages.English;
        }

        if (ChineseRegex().IsMatch(text))
        {
            return TranslationLanguages.Chinese;
        }

        if (JapaneseRegex().IsMatch(text))
        {
            return TranslationLanguages.Japanese;
        }

        if (KoreanRegex().IsMatch(text))
        {
            return TranslationLanguages.Korean;
        }

        if (CyrillicRegex().IsMatch(text))
        {
            return TranslationLanguages.Russian;
        }

        return TranslationLanguages.English;
    }

    /// <summary>
    /// 把"源语言 + 目标语言"解析成一对具体语言，**自动互译的语言对默认是中↔英**。
    /// </summary>
    public static (string From, string To) Resolve(string? from, string? to, string? text)
        => Resolve(from, to, text, TranslationLanguages.Chinese, TranslationLanguages.English);

    /// <summary>
    /// 把"源语言（可能是自动）+ 目标语言（可能是自动 = 另一种语言）"解析成一对具体语言。
    /// <para>
    /// <paramref name="pairA"/>/<paramref name="pairB"/> 是**自动互译用的语言对**（不限于中英，
    /// 俄语使用者可以设成"俄语 ⇄ 英语"或"俄语 ⇄ 中文"）：
    /// 目标语言为自动时，文本是 B 就译成 A、文本是 A 就译成 B、**其它语言统一译成 A**（通常把 A 设成自己的母语）。
    /// 目标语言是具体语言时按它来；目标与源相同时按"译成另一侧"兜底。
    /// </para>
    /// </summary>
    public static (string From, string To) Resolve(string? from, string? to, string? text, string? pairA, string? pairB)
    {
        var source = TranslationLanguages.Normalize(from);
        var target = TranslationLanguages.Normalize(to);

        if (source == TranslationLanguages.Auto)
        {
            source = Detect(text);
        }

        if (target == TranslationLanguages.Auto)
        {
            var a = TranslationLanguages.IsTarget(pairA) ? TranslationLanguages.Normalize(pairA) : TranslationLanguages.Chinese;
            var b = TranslationLanguages.IsTarget(pairB) ? TranslationLanguages.Normalize(pairB) : TranslationLanguages.English;
            if (a == b)
            {
                b = a == TranslationLanguages.Chinese ? TranslationLanguages.English : TranslationLanguages.Chinese;
            }

            target = source == b ? a : (source == a ? b : a);
        }

        if (target == source || !TranslationLanguages.IsTarget(target))
        {
            target = source == TranslationLanguages.Chinese
                ? TranslationLanguages.English
                : TranslationLanguages.Chinese;
        }

        return (source, target);
    }

    /// <summary>语言代码 → 本地化键（供界面显示"英语 → 中文"）。</summary>
    public static string LanguageKey(string? languageCode)
        => TranslationLanguages.DisplayKey(languageCode);
}
