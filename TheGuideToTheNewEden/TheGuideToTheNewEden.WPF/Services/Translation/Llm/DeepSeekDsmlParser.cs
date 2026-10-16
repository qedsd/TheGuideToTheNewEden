using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TheGuideToTheNewEden.WPF.Services.Translation.Llm;

/// <summary>DSML 解析结果。</summary>
/// <param name="Text">去掉工具调用标记后的正文（可直接展示）。</param>
/// <param name="Calls">解析出的工具调用（没有则为空表）。</param>
/// <param name="Detected">文本里是否出现了 DSML 标记。</param>
/// <param name="Truncated">标记是否不完整（缺闭合标签，通常是输出被 token 上限截断）。</param>
/// <param name="Error">需要展示给用户的失败原因（能解析时为 null）。</param>
public sealed record DsmlParseResult(
    string Text,
    IReadOnlyList<ChatToolCall> Calls,
    bool Detected,
    bool Truncated,
    string? Error);

/// <summary>
/// DeepSeek 原生工具调用标记（DSML）的兼容解析器。
/// <para>
/// <b>为什么需要它</b>：DeepSeek V3.2 / V4 系模型（<c>deepseek-flash</c> 等）的**原生**工具调用格式是 DSML 文本，
/// 官方 API 通常会在服务端把它转换成 OpenAI 风格的 <c>tool_calls</c>，但**已知会漏**——标记直接出现在
/// <c>message.content</c> 里（[issue #1678](https://github.com/deepseek-ai/DeepSeek-V3/issues/1678)）。
/// 客户端若不当回事，表现就是"模型把一段 XML 当回答吐出来、工具一次都没调用、对话莫名其妙断掉"（阶段 120 实测）。
/// </para>
/// <para>
/// 语法（V3.2 的外层块名是 <c>function_calls</c>，V4 是 <c>tool_calls</c>，内部一致）：
/// <code>
/// &lt;｜DSML｜tool_calls&gt;
///   &lt;｜DSML｜invoke name="func"&gt;
///     &lt;｜DSML｜parameter name="key" string="true"&gt;value&lt;/｜DSML｜parameter&gt;
///   &lt;/｜DSML｜invoke&gt;
/// &lt;/｜DSML｜tool_calls&gt;
/// </code>
/// 约定：<c>string="true"</c> 的值按字符串取；<c>string="false"</c> 的值先按 JSON 解析（数字/布尔/数组/对象），
/// 失败才退回字符串；<c>invoke</c> 内也允许直接写一整段 JSON（部分实现这么发）。
/// 竖线容忍全角 <c>｜</c>(U+FF5C) 与 ASCII <c>|</c>、单写与双写。
/// </para>
/// </summary>
public static class DeepSeekDsmlParser
{
    /// <summary>DSML 哨兵：<c>&lt;｜DSML｜</c>（竖线是 U+FF5C，用转义写死，避免肉眼分不清全角/半角）。</summary>
    private const string Sentinel = "<\uFF5CDSML\uFF5C";

    /// <summary>句末标记 <c>&lt;｜end▁of▁sentence｜&gt;</c>（▁ 是 U+2581）：截断的输出里会漏进正文/参数。</summary>
    private const string EndOfSentence = "<\uFF5Cend\u2581of\u2581sentence\uFF5C>";

    /// <summary>竖线：全角或 ASCII、单写或双写。</summary>
    private const string Bars = @"[｜|]{1,2}";

    private static readonly Regex BlockRegex = new(
        $@"<{Bars}DSML{Bars}(?<name>tool_calls|function_calls)\s*>",
        RegexOptions.Compiled);

    private static readonly Regex InvokeRegex = new(
        $@"<{Bars}DSML{Bars}invoke\s+name=""(?<name>[^""]*)""\s*>(?<body>.*?)(?:</{Bars}DSML{Bars}invoke\s*>|$)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ParameterRegex = new(
        $@"<{Bars}DSML{Bars}parameter\s+name=""(?<name>[^""]+)""(?:\s+string=""(?<string>true|false)"")?\s*>(?<value>.*?)(?:</{Bars}DSML{Bars}parameter\s*>|$)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>任何 DSML 味道的残留片段（用于清掉截断留下的半个标签）。</summary>
    private static readonly Regex AnyTokenRegex = new($@"[｜|]{{1,2}}DSML[｜|]{{1,2}}", RegexOptions.Compiled);

    private static readonly Regex TrailingFragmentRegex = new(
        $@"(?:</?{Bars}DSML{Bars}[a-z_]*\s*>?|<{Bars}DSML{Bars})$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Dictionary<string, Regex> CloseTagRegexes = new(StringComparer.Ordinal)
    {
        ["tool_calls"] = new Regex($@"</{Bars}DSML{Bars}tool_calls\s*>", RegexOptions.Compiled),
        ["function_calls"] = new Regex($@"</{Bars}DSML{Bars}function_calls\s*>", RegexOptions.Compiled),
    };

    /// <summary>文本里是否出现 DSML 标记（含 ASCII 竖线写法）。</summary>
    public static bool ContainsDsml(string? text)
        => !string.IsNullOrEmpty(text)
           && (text.Contains(Sentinel, StringComparison.Ordinal)
               || text.Contains("<|DSML|", StringComparison.Ordinal));

    /// <summary>
    /// 解析正文里的 DSML 工具调用。没有任何标记时原样返回（<see cref="DsmlParseResult.Detected"/> = false），
    /// 因此可以无条件对每次响应调用一次。
    /// </summary>
    public static DsmlParseResult Parse(string? content)
    {
        var text = content ?? string.Empty;
        if (!ContainsDsml(text))
        {
            return new DsmlParseResult(text, [], false, false, null);
        }

        var calls = new List<ChatToolCall>();
        var kept = new StringBuilder();
        var truncated = false;
        var cursor = 0;

        foreach (Match block in BlockRegex.Matches(text))
        {
            if (block.Index < cursor)
            {
                continue;   // 落在上一个块里（理论上不会），防御
            }

            var blockName = block.Groups["name"].Value;
            var innerStart = block.Index + block.Length;
            var close = CloseTagRegexes[blockName].Match(text, innerStart);
            var innerEnd = close.Success ? close.Index : text.Length;
            if (!close.Success)
            {
                truncated = true;
            }

            // 块之前的正文保留
            kept.Append(text, cursor, block.Index - cursor);
            cursor = close.Success ? close.Index + close.Length : text.Length;

            var inner = text[innerStart..innerEnd];
            foreach (Match invoke in InvokeRegex.Matches(inner))
            {
                var name = invoke.Groups["name"].Value.Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                calls.Add(new ChatToolCall(
                    $"call_dsml_{calls.Count + 1}",
                    name,
                    BuildArguments(invoke.Groups["body"].Value, ref truncated)));
            }
        }

        if (cursor < text.Length)
        {
            kept.Append(text, cursor, text.Length - cursor);
        }

        var visible = kept.ToString();

        // 标记没闭合干净（例如 <｜DSML｜tool 就被截断）→ 从残留标记处截掉，并标记为截断
        var strayToken = AnyTokenRegex.Match(visible);
        if (strayToken.Success)
        {
            visible = visible[..strayToken.Index];
            truncated = true;
        }

        visible = Clean(visible);

        var error = calls.Count > 0
            ? null
            : truncated
                ? "模型返回的工具调用标记不完整（很可能被「最大输出 token」截断）。请在 设置 → AI 助手 调大该值后重试。"
                : "模型返回了工具调用标记，但里面没有可解析的调用。请重试一次。";

        return new DsmlParseResult(visible, calls, true, truncated, error);
    }

    /// <summary>把 invoke 体转成参数 JSON：优先"整段 JSON"，否则解析 parameter 子标签。</summary>
    private static string BuildArguments(string body, ref bool truncated)
    {
        var trimmed = StripNoise(body).Trim();
        if (trimmed.StartsWith('{'))
        {
            // 直接 JSON（少数实现这么发）
            if (!trimmed.EndsWith('}'))
            {
                truncated = true;
            }

            return trimmed;
        }

        var parameters = new JsonObject();
        foreach (Match parameter in ParameterRegex.Matches(body))
        {
            var name = parameter.Groups["name"].Value;
            if (name.Length == 0)
            {
                continue;
            }

            var raw = StripNoise(parameter.Groups["value"].Value);
            if (!parameter.Groups["string"].Success
                || string.Equals(parameter.Groups["string"].Value, "true", StringComparison.OrdinalIgnoreCase))
            {
                parameters[name] = JsonValue.Create(raw);
                continue;
            }

            // string="false"：数字/布尔/数组/对象按 JSON 取，解析不出来才当字符串
            parameters[name] = TryParseJson(raw) ?? JsonValue.Create(raw.Trim());
        }

        return parameters.ToJsonString();
    }

    private static JsonNode? TryParseJson(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(trimmed);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>去掉句末标记与残留的半个标签（截断输出常见）。</summary>
    private static string StripNoise(string value)
        => TrailingFragmentRegex.Replace(
            value.Replace(EndOfSentence, string.Empty, StringComparison.Ordinal),
            string.Empty);

    private static string Clean(string value)
        => StripNoise(value).Trim();
}
