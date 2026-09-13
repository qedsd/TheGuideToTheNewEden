using System.Text.RegularExpressions;

namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>翻译语言判定（本地数据库只区分中文 / 英文）。</summary>
public static partial class TranslationLanguageHelper
{
    /// <summary>CJK 统一表意文字：出现即视为中文。</summary>
    [GeneratedRegex(@"[\u4e00-\u9fff]")]
    private static partial Regex ChineseRegex();

    /// <summary>文本里是否含中文。</summary>
    public static bool ContainsChinese(string? text)
        => !string.IsNullOrEmpty(text) && ChineseRegex().IsMatch(text);

    /// <summary>把方向解析为具体的中英方向（<see cref="TranslationDirection.Auto"/> 按文本内容判定）。</summary>
    public static TranslationDirection Resolve(TranslationDirection direction, string? text)
    {
        if (direction != TranslationDirection.Auto)
        {
            return direction;
        }

        return ContainsChinese(text)
            ? TranslationDirection.ChineseToEnglish
            : TranslationDirection.EnglishToChinese;
    }

    /// <summary>方向 → 本地化键（形如「英文 → 中文」）。</summary>
    public static string DirectionKey(TranslationDirection direction)
        => direction == TranslationDirection.ChineseToEnglish
            ? "TranslationPage_Direction_ZhToEn"
            : "TranslationPage_Direction_EnToZh";

    /// <summary>Core 的语言代码 → 本地化键（沿用旧在线翻译接口的代码取值）。</summary>
    public static string LanguageKey(string? languageCode)
        => string.IsNullOrEmpty(languageCode)
            ? "TranslationPage_EN"
            : languageCode.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                ? "TranslationPage_Chinese"
                : "TranslationPage_EN";
}
