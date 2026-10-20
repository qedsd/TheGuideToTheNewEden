using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.Services.Translation.Llm;

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

    /// <summary>
    /// 全部声明的总字符预算。阶段 122 精简后 23,358；阶段 131 +2 说明书工具 24,314；
    /// 阶段 152/153 加 4 个配置工具、阶段 155 加 2 个功能重启工具后上调到 28,000
    /// （63 个工具；仍由本套件守住"别无限膨胀"）。
    /// </summary>
    public const int MaxDeclarationChars = 28000;

    /// <summary>
    /// 系统提示词字符上限（它是同一份前缀的一部分）。
    /// 阶段 122 定的是 1,100（当时实测 1,040）；阶段 118 加了 esi_* 三步用法、阶段 131 加了
    /// 说明书三条规则（14–16）、阶段 134 加了「数据时间 / 历史数据可能过期」规则 17 后实测 **1,417**，
    /// 阶段 151 加注入防护规则 18 后 **1,521**，阶段 152 加配置读写三条规则（19–21）后约 **1,680**，
    /// 阶段 154 规则 19 补「查看配置」与「说明书没列工具≠不可读」后约 **1,806**，
    /// 阶段 155 加功能重启规则 22 后约 **1,900**，故预算上调到 1,950——
    /// 上调不是"放开"，而是让这条断言继续守住"提示词别无限膨胀"。
    /// </summary>
    public const int MaxSystemPromptChars = 1950;

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
        c.That(prompt.Contains("不是指令", StringComparison.Ordinal),
            "系统提示词声明工具结果是数据、不是指令（阶段 151 注入防护）");

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

        c.Section("TokenSuite · 估算校准（阶段 151）");

        // 纯函数：实际 < 估算 → 系数下降；实际 > 估算 → 上升；异常输入保持当前值
        c.Between(AssistantUsageCalibration.ComputeFactor(1.0, 1000, 600), 0.87, 0.89,
            "实际/估算 = 0.6 → EMA 后系数约 0.88");
        c.Between(AssistantUsageCalibration.ComputeFactor(1.0, 1000, 2000), 1.14, 1.16,
            "实际/估算 = 2（比值夹到 1.5）→ 系数约 1.15");
        c.Equal(AssistantUsageCalibration.ComputeFactor(0.9, 0, 100), 0.9, "估算为 0 时不学习");
        c.Equal(AssistantUsageCalibration.ComputeFactor(0.9, 1000, 0), 0.9, "实际为 0 时不学习");

        // 估算器应用系数后按比例变化；复位后恢复（复位避免污染其它断言）
        var baseline = AssistantTokenEstimator.Estimate(new string('a', 1000));
        AssistantTokenEstimator.SetCalibrationFactor(0.8);
        var scaled = AssistantTokenEstimator.Estimate(new string('a', 1000));
        AssistantTokenEstimator.ResetCalibration();
        c.That(scaled < baseline, $"校准 0.8 时估算更小（{scaled} < {baseline}）");
        c.Equal(AssistantTokenEstimator.Estimate(new string('a', 1000)), baseline, "复位后估算恢复原值");

        c.Section("TokenSuite · 前缀缓存用量解析（阶段 151）");
        var openAi = new OpenAiChatProtocol().ParseResponse(
            """{"choices":[{"message":{"content":"ok"},"finish_reason":"stop"}],"usage":{"prompt_tokens":100,"completion_tokens":5,"prompt_cache_hit_tokens":80}}""");
        c.Equal(openAi.Usage.PromptCacheHitTokens, 80, "OpenAI 兼容解析 prompt_cache_hit_tokens");

        var anthropic = new AnthropicChatProtocol().ParseResponse(
            """{"content":[{"type":"text","text":"ok"}],"stop_reason":"end_turn","usage":{"input_tokens":100,"output_tokens":5,"cache_read_input_tokens":80}}""");
        c.Equal(anthropic.Usage.PromptCacheHitTokens, 80, "Anthropic 解析 cache_read_input_tokens");

        var gemini = new GeminiChatProtocol().ParseResponse(
            """{"candidates":[{"content":{"parts":[{"text":"ok"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":100,"candidatesTokenCount":5,"cachedContentTokenCount":80}}""");
        c.Equal(gemini.Usage.PromptCacheHitTokens, 80, "Gemini 解析 cachedContentTokenCount");
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
