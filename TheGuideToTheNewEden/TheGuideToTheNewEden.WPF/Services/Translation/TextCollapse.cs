using System.Text;

namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>
/// 文本的"显示行数预算"：按**半角字符宽**估算一段文本会占多少显示行，并能截出前 N 行的预览。
/// <para>
/// AI 翻译页的气泡与频道翻译的实时列表都用它（原文 3 行 / 译文 5 行那套折叠逻辑），
/// 免得两处各写一份"数行数 + 截预览"的代码。宽度单位是估算值（半角 1、CJK 2），
/// 行宽由调用方按气泡/列表的实际宽度给（例如 560px 气泡 ≈ 74 单位/行）。
/// </para>
/// </summary>
public static class TextCollapse
{
    /// <summary>统一换行符，方便逐字符扫描。</summary>
    public static string Normalize(string? text)
        => (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');

    /// <summary>估算显示行数（换行符算一次换行，超宽自动折行）。</summary>
    public static int CountLines(string? text, double unitsPerLine)
    {
        var lines = 1;
        var lineUnits = 0.0;
        foreach (var ch in Normalize(text))
        {
            if (ch == '\n')
            {
                lines++;
                lineUnits = 0;
                continue;
            }

            var unit = IsWide(ch) ? 2.0 : 1.0;
            if (lineUnits + unit > unitsPerLine)
            {
                lines++;
                lineUnits = 0;
            }

            lineUnits += unit;
        }

        return lines;
    }

    /// <summary>取前 <paramref name="maxLines"/> 行的文本（写满就停），末尾补省略号。</summary>
    public static string BuildPreview(string text, int maxLines, double unitsPerLine)
    {
        var builder = new StringBuilder();
        var lines = 1;
        var lineUnits = 0.0;

        foreach (var ch in Normalize(text))
        {
            if (ch == '\n')
            {
                lines++;
                if (lines > maxLines)
                {
                    break;
                }

                builder.Append('\n');
                lineUnits = 0;
                continue;
            }

            var unit = IsWide(ch) ? 2.0 : 1.0;
            if (lineUnits + unit > unitsPerLine)
            {
                lines++;
                if (lines > maxLines)
                {
                    break;
                }

                builder.Append('\n');
                lineUnits = 0;
            }

            builder.Append(ch);
            lineUnits += unit;
        }

        return builder.ToString().TrimEnd() + "…";
    }

    /// <summary>宽字符（CJK / 全角标点）：按两个半角宽计。</summary>
    public static bool IsWide(char ch)
        => ch >= 0x1100 && (ch <= 0x115F
            || ch == 0x2329 || ch == 0x232A
            || (ch >= 0x2E80 && ch <= 0xA4CF && ch != 0x303F)
            || (ch >= 0xAC00 && ch <= 0xD7A3)
            || (ch >= 0xF900 && ch <= 0xFAFF)
            || (ch >= 0xFE30 && ch <= 0xFE6F)
            || (ch >= 0xFF00 && ch <= 0xFF60)
            || (ch >= 0xFFE0 && ch <= 0xFFE6));
}
