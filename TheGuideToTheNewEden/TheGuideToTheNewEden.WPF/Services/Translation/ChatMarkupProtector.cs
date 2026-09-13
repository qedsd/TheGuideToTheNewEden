using System.Text;
using System.Text.RegularExpressions;

namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>
/// 聊天文本的**预清洗**：把游戏内 HTML 风格的标记在送翻译**之前**去掉。
/// <para>
/// 处理规则（顺序固定）：
/// <list type="number">
///   <item><c>&lt;br&gt;</c> / <c>&lt;br/&gt;</c> → 换行（保留 MOTD 的段落结构，译文也能按行读）；</item>
///   <item>其余 <c>&lt;...&gt;</c> 标签（<c>&lt;font size= color=&gt;</c>、<c>&lt;b&gt;</c>、<c>&lt;loc&gt;</c>、
///         <c>&lt;a href="joinChannel:..."&gt;</c> 等）**整体删掉**，标签之间的正文保留；</item>
///   <item>常见 HTML 实体解码（<c>&amp;amp; &amp;lt; &amp;gt; &amp;quot; &amp;#39; &amp;nbsp;</c>）；</item>
///   <item>逐行去尾空白、连续空行压到最多一个空行、整体 Trim。</item>
/// </list>
/// </para>
/// <para>
/// 为什么不再用"占位符保护 + 还原"：模型经常丢占位符（实测一段 92 个标签的 MOTD 就是典型场景），
/// 而颜色/字号/链接这些标记对**翻译**没有任何价值——先删掉更稳、prompt 更短、译文直接可读。
/// </para>
/// </summary>
public static class ChatMarkupProtector
{
    /// <summary>匹配一个完整的 <c>&lt;...&gt;</c> 标签（含闭合标签）；要求 <c>&lt;</c> 后紧跟字母，避免误伤数学比较。</summary>
    private static readonly Regex TagRegex = new(@"</?[A-Za-z][^<>]*>", RegexOptions.Compiled);

    private static readonly Regex BreakRegex = new(@"<br\s*/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BlankLinesRegex = new(@"\n[ \t]*\n[ \t]*\n+", RegexOptions.Compiled);

    private static readonly Regex SpacesRegex = new(@"[ \t]{2,}", RegexOptions.Compiled);

    /// <summary>文本里是否含任何 <c>&lt;...&gt;</c> 标记。</summary>
    public static bool HasMarkup(string? text) => !string.IsNullOrEmpty(text) && TagRegex.IsMatch(text);

    /// <summary>清洗成可送翻译的纯文本。</summary>
    public static string ToPlainText(string? text) => ToPlainText(text, out _);

    /// <summary>
    /// 清洗成可送翻译的纯文本，并回报删掉了多少标签（用于界面提示"已去除游戏内标记 N 个"）。
    /// </summary>
    public static string ToPlainText(string? text, out int removedTags)
    {
        removedTags = 0;
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // 1) <br> → 换行（先做，避免被当普通标签直接删掉而丢失断行）
        var result = BreakRegex.Replace(text, "\n");

        // 2) 其余标签整体删除（先在普通循环里计数，闭包里不能写 out 参数）
        var tagMatches = TagRegex.Matches(result);
        if (tagMatches.Count > 0)
        {
            removedTags = tagMatches.Count;
            result = TagRegex.Replace(result, string.Empty);
        }

        // 3) 常见实体
        result = result
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("&lt;", "<", StringComparison.OrdinalIgnoreCase)
            .Replace("&gt;", ">", StringComparison.OrdinalIgnoreCase)
            .Replace("&quot;", "\"", StringComparison.OrdinalIgnoreCase)
            .Replace("&#39;", "'", StringComparison.OrdinalIgnoreCase)
            .Replace("&apos;", "'", StringComparison.OrdinalIgnoreCase)
            .Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase);

        // 4) 逐行去尾空白 → 压缩连续空行与多空格 → 整体 Trim
        var lines = result
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(p => SpacesRegex.Replace(p.Trim(), " "));

        result = string.Join('\n', lines);
        result = BlankLinesRegex.Replace(result, "\n\n");
        return result.Trim();
    }

    /// <summary>纯文本的单行形式（列表里展示原文/译文时用，避免 MOTD 那种多行内容把行高撑得很大）。</summary>
    public static string ToSingleLine(string? text, int maxLength = 0)
    {
        var normalized = (text ?? string.Empty)
            .Replace("\r\n", " ")
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Trim();
        normalized = SpacesRegex.Replace(normalized, " ");
        return maxLength > 0 && normalized.Length > maxLength ? normalized[..maxLength] + "…" : normalized;
    }

    /// <summary>取出文本里的标签（诊断用）。</summary>
    public static string ExtractTags(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (Match match in TagRegex.Matches(text))
        {
            builder.Append(match.Value);
        }

        return builder.ToString();
    }
}
