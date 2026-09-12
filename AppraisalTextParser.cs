using System.Text.RegularExpressions;

namespace TheGuideToTheNewEden.WPF.Services.Business;

/// <summary>
/// 估价输入的一行解析结果。
/// </summary>
public sealed class AppraisalParsedLine
{
    /// <summary>原始输入行（未识别时用于提示）。</summary>
    public string RawLine { get; init; } = string.Empty;

    /// <summary>解析出的物品名（可能为空）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>数量；未能识别数量时为 1。</summary>
    public double Amount { get; set; } = 1;
}

/// <summary>
/// 把游戏里复制出来的多行文本（合同物品列表、货柜/机库全选复制、资产清单等）
/// 解析成 物品名 + 数量 列表，供估价服务再按本地 SDE 映射到类型 ID。
/// <para>
/// 支持的行格式（按优先级）：
/// 1. Tab 分隔（货柜/机库"全选复制"）：<c>物品名\t数量\t体积\t分组</c>；
/// 2. 合同物品列表：<c>物品名 x数量</c>；
/// 3. 习惯写法：<c>数量x 物品名</c>；
/// 4. 行尾独立数字：<c>物品名 数量</c>；
/// 5. 兜底：整行为物品名，数量 1。
/// 数量支持千分位逗号；物品名里的 <c>*</c> 标记（部分扫描/导出格式会在名字后附加，如
/// <c>导弹精确打击脚本*\t2</c>）会被清理——SDE 物品名本身不含 <c>*</c>。
/// </para>
/// </summary>
public static partial class AppraisalTextParser
{
    /// <summary>行尾 " x数量"（合同格式）；x 前必须有空格，避免误匹配名称里含 x 的部分。</summary>
    [GeneratedRegex(@"\s+[xX×]\s*([\d,]+(?:\.\d+)?)\s*$")]
    private static partial Regex XSuffixRegex();

    /// <summary>行首 "数量x "（部分工具/击杀报告的写法）；x 后必须有空格。</summary>
    [GeneratedRegex(@"^\s*([\d,]+(?:\.\d+)?)\s*[xX×]\s+(.+)$")]
    private static partial Regex XPrefixRegex();

    /// <summary>行尾独立的纯数字（可能是数量，也可能只是名字的一部分，最后兜底）。</summary>
    [GeneratedRegex(@"\s+([\d,]+(?:\.\d+)?)$")]
    private static partial Regex TrailingNumberRegex();

    public static List<AppraisalParsedLine> Parse(string input)
    {
        var lines = new List<AppraisalParsedLine>();
        if (string.IsNullOrWhiteSpace(input))
        {
            return lines;
        }

        foreach (var raw in input.Split('\n'))
        {
            var line = raw.Trim('\r').Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var parsed = ParseLine(line);
            if (parsed is not null)
            {
                parsed.Name = CleanName(parsed.Name);
                if (parsed.Name.Length > 0)
                {
                    lines.Add(parsed);
                }
            }
        }

        return lines;
    }

    /// <summary>
    /// 清理物品名：去掉 <c>*</c> 标记（部分扫描/导出格式会在名字前后附加，SDE 物品名本身不含该字符）。
    /// </summary>
    private static string CleanName(string name)
        => name.Replace("*", string.Empty).Trim();

    private static AppraisalParsedLine? ParseLine(string line)
    {
        // 1. Tab 分隔：第 1 列名称、第 2 列数量（货柜/机库全选复制，数量带千分位逗号）
        var tabs = line.Split('\t');
        if (tabs.Length > 1)
        {
            var name = tabs[0].Trim();
            if (name.Length == 0)
            {
                return null;
            }

            var amount = tabs.Length > 1 ? ParseAmount(tabs[1]) : 1;
            return new AppraisalParsedLine { RawLine = line, Name = name, Amount = amount };
        }

        // 2. 合同物品列表："物品名 x数量"
        var match = XSuffixRegex().Match(line);
        if (match.Success && match.Index > 0)
        {
            return new AppraisalParsedLine
            {
                RawLine = line,
                Name = line[..match.Index].Trim(),
                Amount = ParseAmount(match.Groups[1].Value),
            };
        }

        // 3. "数量x 物品名"
        match = XPrefixRegex().Match(line);
        if (match.Success)
        {
            return new AppraisalParsedLine
            {
                RawLine = line,
                Name = match.Groups[2].Value.Trim(),
                Amount = ParseAmount(match.Groups[1].Value),
            };
        }

        // 4. 行尾独立数字："物品名 数量"。仅当去掉数字后名称仍有内容才采用。
        match = TrailingNumberRegex().Match(line);
        if (match.Success && match.Index > 0)
        {
            var name = line[..match.Index].Trim();
            if (name.Length > 0)
            {
                return new AppraisalParsedLine
                {
                    RawLine = line,
                    Name = name,
                    Amount = ParseAmount(match.Groups[1].Value),
                };
            }
        }

        // 5. 兜底：整行为物品名
        return new AppraisalParsedLine { RawLine = line, Name = line, Amount = 1 };
    }

    /// <summary>数量解析：去掉千分位逗号后取 double；解析失败按 1 处理。</summary>
    private static double ParseAmount(string text)
    {
        var cleaned = text.Trim().Replace(",", string.Empty);
        if (double.TryParse(cleaned, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            && value > 0)
        {
            return value;
        }

        return 1;
    }
}
