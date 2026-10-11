using System.Text.RegularExpressions;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// 输入框的"取词"规则（移植自 WinUI 版 <c>IMEPage</c> 里的那几个正则与索引逻辑）。
/// <para>
/// 与 WinUI 版的差异：<b>修掉了越界崩溃</b>——原版直接算 <c>SelectionStart - 1</c>，
/// 光标在第 0 位（例如按了 Home）时读到 <c>text[-1]</c>，会抛 <c>ArgumentOutOfRangeException</c>；
/// 另外原版把"光标前一个字符"当作正在输入的词尾，这里保持同一语义但把索引夹进合法范围。
/// </para>
/// </summary>
public static class InputTextHelper
{
    /// <summary>纯数字。</summary>
    private static readonly Regex PureDigitsRegex = new(@"^\d+$", RegexOptions.Compiled);

    /// <summary>带可选正负号的整数。</summary>
    private static readonly Regex IntegerRegex = new(@"^[+-]?\d+$", RegexOptions.Compiled);

    /// <summary>小数（带可选正负号）。</summary>
    private static readonly Regex DecimalRegex = new(@"^[+-]?\d*\.?\d+$", RegexOptions.Compiled);

    /// <summary>严格小数（必须有小数点）。</summary>
    private static readonly Regex StrictDecimalRegex = new(@"^[+-]?\d+\.\d+$", RegexOptions.Compiled);

    /// <summary>英文标点 / 符号 / 空白。</summary>
    private static readonly Regex SeparatorRegex = new(@"^[\p{P}\p{S}\s]$", RegexOptions.Compiled);

    /// <summary>中文标点（含全角符号、破折号、省略号、间隔号）。</summary>
    private static readonly Regex ChineseSeparatorRegex = new(@"^[\u3000-\u303F\uFF00-\uFFEF\u2014\u2026\u00B7\u30FB]$", RegexOptions.Compiled);

    /// <summary>是否包含汉字。</summary>
    private static readonly Regex ChineseRegex = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    public static bool IsSeparator(char c)
        => SeparatorRegex.IsMatch(c.ToString()) || ChineseSeparatorRegex.IsMatch(c.ToString());

    public static bool IsNumber(string? text)
        => !string.IsNullOrEmpty(text)
           && (PureDigitsRegex.IsMatch(text)
               || IntegerRegex.IsMatch(text)
               || DecimalRegex.IsMatch(text)
               || StrictDecimalRegex.IsMatch(text));

    public static bool IsChinese(string? text)
        => !string.IsNullOrEmpty(text) && ChineseRegex.IsMatch(text);

    /// <summary>
    /// 取"光标处正在输入的词"：以光标**前一个字符**为词尾（与 WinUI 一致），向两侧扩展到分隔符为止。
    /// 纯数字的词不算（不需要名称建议），返回 false。
    /// </summary>
    public static bool TryGetWordAt(string? text, int caretIndex, out string word, out int start, out int end)
    {
        word = string.Empty;
        start = -1;
        end = -1;

        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        // 光标可能在第 0 位（WinUI 版这里会越界崩）
        var index = System.Math.Clamp(caretIndex - 1, 0, text.Length - 1);
        if (IsSeparator(text[index]))
        {
            return false;
        }

        start = 0;
        end = text.Length - 1;

        for (var i = index; i >= 0; i--)
        {
            if (IsSeparator(text[i]))
            {
                start = i + 1;
                break;
            }
        }

        for (var i = index; i < text.Length; i++)
        {
            if (IsSeparator(text[i]))
            {
                end = i - 1;
                break;
            }
        }

        if (end < start)
        {
            return false;
        }

        word = text.Substring(start, end - start + 1);
        return word.Length > 0 && !IsNumber(word);
    }
}
