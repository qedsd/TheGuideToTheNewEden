using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TheGuideToTheNewEden.WPF.Services.Assistant;

namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// 阶段 122：工具声明的 **token 预算** 与"缓存友好"约束。
/// <para>
/// 为什么要有这一套：46 个工具的声明是**每次工具轮请求的固定前缀**（阶段 131 实测 24,314 字符 ≈ 0.8–1 万 token，
/// 占那一轮输入的绝大部分）。而 DeepSeek 的上下文缓存按**前缀逐字节匹配**计费——
/// 命中 0.02 元/M、未命中 1 元/M（相差 50 倍）。所以两件事必须被守住：
/// ① **前缀要稳定**（顺序固定、描述里不放易变内容），否则缓存从变化处起全部失效；
/// ② **前缀要短**（描述只讲"何时用 + 关键限制"，不复述参数表——参数就在 schema 里；参数描述压到一句话）。
/// </para>
/// <para>纯离线、只读：不碰 settings.json，不发请求。</para>
/// </summary>
internal static class TokenSuite
{
    /// <summary>单条工具描述字符上限（够说清"何时用 + 一个例子"）。</summary>
    public const int MaxToolDescriptionChars = 140;

    /// <summary>单条参数描述字符上限。</summary>
    public const int MaxParamDescriptionChars = 24;

    /// <summary>46 条声明的总字符预算（阶段 122 精简后 23,358；阶段 131 加两个说明书工具后实测 24,314）。</summary>
    public const int MaxDeclarationChars = 26000;

    /// <summary>
    /// 系统提示词字符上限（它是同一份前缀的一部分）。
    /// 阶段 122 定的是 1,100（当时实测 1,040）；阶段 118 加了 esi_* 三步用法、阶段 131 加了
    /// 说明书三条规则（14–16）后实测 **1,317**，故预算上调到 1,400 并留一点余量——
    /// 上调不是"放开"，而是让这条断言继续守住"提示词别无限膨胀"（再加规则就会顶到上限）。
    /// </summary>
    public const int MaxSystemPromptChars = 1400;

    /// <summary>
    /// 易变内容：**嵌进描述里的会变的值**（日期 / 年份 / "最新版本"之类）。
    /// 注意不要把"现在/目前"这种叙述词算进来——它们是常量字符串的一部分，不会让前缀变化
    /// （第一版把它们算上，7 条描述被误报，已收窄）。
    /// </summary>
    private static readonly Regex Volatile = new(
        @"\d{4}\s*年|\d{4}-\d{2}-\d{2}|\d{4}/\d{1,2}/\d{1,2}|今天|昨天|最新版本|本版本|更新时间|发布版本",
        RegexOptions.Compiled);

    public static void Run(Check c)
    {
        c.Section("TokenSuite · 声明规模（固定前缀 = 每次工具轮都要重发）");

        // 只看**产品工具**：probe_* 是探针自己的替身，不进用户请求
        var all = ToolRegistry.FilterSchemas(
            ToolRegistry.All.Where(t => !t.Name.StartsWith("probe_", StringComparison.Ordinal)), null);
        var rows = all.Select(t => new
        {
            t.Name,
            Desc = t.Description ?? string.Empty,
            Schema = t.Parameters?.ToJsonString() ?? "{}",
            Parameters = t.Parameters,
        }).ToList();

        foreach (var row in rows)
        {
            var total = row.Desc.Length + row.Schema.Length + row.Name.Length + 70;
            var label = $"{row.Name}（描述 {row.Desc.Length} / schema {row.Schema.Length} / 合计 {total}）";
            if (row.Desc.Length > MaxToolDescriptionChars)
            {
                c.That(false, $"{label}：描述超过 {MaxToolDescriptionChars} 字符");
            }
        }

        c.That(rows.All(r => r.Desc.Length <= MaxToolDescriptionChars),
            $"{rows.Count} 条工具描述都不超过 {MaxToolDescriptionChars} 字符（最长 {rows.Max(r => r.Desc.Length)}）");
        c.That(rows.All(r => r.Desc.Length >= 20), "每条描述都够具体（≥20 字符，模型靠它选工具）");

        var totalChars = rows.Sum(r => r.Desc.Length + r.Schema.Length + r.Name.Length + 70);
        c.That(totalChars <= MaxDeclarationChars,
            $"声明总字符 {totalChars} ≤ 预算 {MaxDeclarationChars}（最长 10 条见下方诊断）");
        if (totalChars > MaxDeclarationChars)
        {
            foreach (var row in rows.OrderByDescending(r => r.Desc.Length + r.Schema.Length).Take(10))
            {
                Console.WriteLine($"    [诊断] {row.Name}：描述 {row.Desc.Length} + schema {row.Schema.Length} 字符");
            }
        }

        c.Section("TokenSuite · 缓存友好（前缀一改，缓存从改处起失效）");

        // 顺序必须确定且稳定：同一份设置下两次构造要完全一致（现在是按名字排序，改动这里要知道代价）
        var first = ToolRegistry.EnabledSchemas.Select(t => t.Name).ToList();
        var second = ToolRegistry.EnabledSchemas.Select(t => t.Name).ToList();
        c.That(first.SequenceEqual(second), "同一次运行里两次取声明，顺序与内容完全一致");
        c.That(first.SequenceEqual(first.OrderBy(n => n, StringComparer.Ordinal)),
            "声明按工具名字典序排列（稳定序 = 缓存可复用；不要改成按「常用度」或时间排序）");

        c.That(rows.All(r => !Volatile.IsMatch(r.Desc)),
            "工具描述里没有易变内容（年份/日期/「现在·目前」这类词）——它们会让缓存永远无法命中");
        foreach (var row in rows.Where(r => Volatile.IsMatch(r.Desc)))
        {
            Console.WriteLine($"    [诊断] {row.Name} 描述里命中易变词：{Volatile.Match(row.Desc).Value}");
        }
        var prompt = AiAssistantEngine.DefaultSystemPrompt;
        c.That(!Volatile.IsMatch(prompt), "系统提示词里没有易变内容");
        c.That(prompt.Length <= MaxSystemPromptChars,
            $"系统提示词 {prompt.Length} ≤ {MaxSystemPromptChars} 字符");

        c.Section("TokenSuite · 描述文体（不复述参数表：参数就在 schema 里）");

        var offenders = new List<string>();
        foreach (var row in rows)
        {
            // 豁免：ESI 网关四个工具是"陌生 API 的说明书"，允许点名 operation / path / query / body，
            // 否则模型不知道往哪儿放参数（其余工具的参数名只该出现在 schema 里）。
            var isGatewayManual = row.Name.StartsWith("esi_", StringComparison.Ordinal);
            if (!isGatewayManual)
            {
                foreach (var parameter in ParameterNames(row.Parameters))
                {
                    if (parameter.Length >= 4 && row.Desc.Contains(parameter, StringComparison.Ordinal))
                    {
                        offenders.Add($"{row.Name} 描述里出现了参数名 {parameter}");
                    }
                }
            }

            foreach (var (parameter, description) in ParameterDescriptions(row.Parameters))
            {
                // ESI 网关四个工具的参数描述允许到 32 字符（它们要交代 operationId 这类陌生概念）
                var cap = isGatewayManual ? 32 : MaxParamDescriptionChars;
                if (description.Length > cap)
                {
                    offenders.Add($"{row.Name}.{parameter} 参数描述 {description.Length} 字符（>{cap}）");
                }
            }
        }

        foreach (var offender in offenders.Take(40))
        {
            Console.WriteLine($"    [诊断] {offender}");
        }

        c.That(offenders.Count == 0, $"没有被点名的文体问题（实际 {offenders.Count} 条，见上方诊断）");
    }

    private static IEnumerable<string> ParameterNames(JsonObject? parameters)
    {
        if (parameters?["properties"] is not JsonObject properties)
        {
            yield break;
        }

        foreach (var pair in properties)
        {
            yield return pair.Key;
        }
    }

    private static IEnumerable<(string Name, string Description)> ParameterDescriptions(JsonObject? parameters)
    {
        if (parameters?["properties"] is not JsonObject properties)
        {
            yield break;
        }

        foreach (var pair in properties)
        {
            if (pair.Value is JsonObject spec && spec["description"]?.GetValue<string>() is { } text)
            {
                yield return (pair.Key, text);
            }
        }
    }
}
