using System.Text;
using TheGuideToTheNewEden.WPF.Services.Translation.Llm;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>
/// 粗略的 token 估算（阶段 134）：**不依赖具体分词器**，用"中日韩字符按 token、其余按字符数/3.2"
/// 的经验公式在发送前把请求体折算成 token，供上下文预算与窗口护栏使用。
/// <para>
/// 为什么要估算而不是等 provider 报错：上下文超限的 400 在用户眼里就是"对话突然失败"，
/// 而预算必须扣掉系统提示词、工具声明、输出预留这些**不在历史里**的部分才准确；
/// 各家分词器不同，这里统一**宁可高估**（高估只会让历史少带几轮，低估则直接超窗）。
/// </para>
/// <para>
/// 口径：CJK 字符 ≈ 0.85 token/字（DeepSeek 系约 0.6，留余量）；其余字符 ≈ 3.2 字符/token；
/// 每条消息再加 <see cref="MessageOverheadTokens"/>（role/分隔符等协议开销）。
/// 单独出现的数字/英文很短，误差可忽略；纯离线纯函数，探针可直接断言。
/// </para>
/// </summary>
public static class AssistantTokenEstimator
{
    /// <summary>CJK 字符的 token 系数（保守高估）。</summary>
    public const double CjkTokensPerChar = 0.85;

    /// <summary>非 CJK 文本每 token 的字符数。</summary>
    public const double OtherCharsPerToken = 3.2;

    /// <summary>每条消息的协议开销（role、分隔符、JSON 转义等）。</summary>
    public const int MessageOverheadTokens = 4;

    /// <summary>估算校准系数的下限（阶段 151；真实用量远小于估算时的最大折扣）。</summary>
    public const double MinCalibrationFactor = 0.6;

    /// <summary>估算校准系数的上限（阶段 151；估算偏低时的最大放大）。</summary>
    public const double MaxCalibrationFactor = 1.5;

    /// <summary>
    /// 全局估算校准系数（阶段 151）：由真实 usage 的"实际输入 / 估算输入"滚动学习得到，
    /// 默认 1.0（不校准）。只影响 <see cref="Estimate"/> 的文本换算，不影响协议开销常数。
    /// </summary>
    public static double CalibrationFactor { get; private set; } = 1.0;

    /// <summary>设置校准系数（自动夹在 <see cref="MinCalibrationFactor"/>–<see cref="MaxCalibrationFactor"/>）。</summary>
    public static void SetCalibrationFactor(double factor)
        => CalibrationFactor = Math.Clamp(factor, MinCalibrationFactor, MaxCalibrationFactor);

    /// <summary>恢复 1.0（探针与"恢复默认"用）。</summary>
    public static void ResetCalibration() => CalibrationFactor = 1.0;

    /// <summary>估算一段文本的 token 数（空文本为 0）。</summary>
    public static int Estimate(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var cjk = 0;
        foreach (var ch in text)
        {
            if (IsCjk(ch))
            {
                cjk++;
            }
        }

        var other = text.Length - cjk;
        var tokens = (cjk * CjkTokensPerChar + other / OtherCharsPerToken) * CalibrationFactor;
        return (int)Math.Ceiling(tokens) + 1;
    }

    /// <summary>估算消息序列（正文 + 工具调用名与参数 + 每条的协议开销）。</summary>
    public static int EstimateMessages(IEnumerable<ChatMessage>? messages)
    {
        if (messages is null)
        {
            return 0;
        }

        var total = 0;
        foreach (var message in messages)
        {
            total += Estimate(message.Content) + MessageOverheadTokens;
            if (message.ToolCalls is { Count: > 0 } calls)
            {
                foreach (var call in calls)
                {
                    total += Estimate(call.Name) + Estimate(call.ArgumentsJson);
                }
            }
        }

        return total;
    }

    /// <summary>估算工具声明（名字 + 描述 + 参数 JSON）。</summary>
    public static int EstimateTools(IEnumerable<ChatTool>? tools)
    {
        if (tools is null)
        {
            return 0;
        }

        var total = 0;
        foreach (var tool in tools)
        {
            total += Estimate(tool.Name) + Estimate(tool.Description);
            if (tool.Parameters is { } parameters)
            {
                total += Estimate(parameters.ToJsonString());
            }
        }

        return total;
    }

    /// <summary>估算一次请求的输入 token（系统提示词 + 消息 + 工具声明）。</summary>
    public static int EstimateRequest(
        string? systemPrompt,
        IEnumerable<ChatMessage>? messages,
        IEnumerable<ChatTool>? tools = null)
        => Estimate(systemPrompt) + EstimateMessages(messages) + EstimateTools(tools);

    /// <summary>该字符是否按 CJK/全角计（中日韩文字与标点、谚文）。</summary>
    public static bool IsCjk(char ch)
        => ch is >= '\u1100' and <= '\u11FF'          // 谚文字母
            or >= '\u2E80' and <= '\u9FFF'            // 部首、标点、假名、统一汉字
            or >= '\uAC00' and <= '\uD7AF'            // 谚文音节
            or >= '\uF900' and <= '\uFAFF'            // 兼容汉字
            or >= '\uFE30' and <= '\uFE4F'            // 兼容形式
            or >= '\uFF00' and <= '\uFFEF';           // 全角字符

    /// <summary>人类可读的紧凑数字（1,234 → 1.2k），日志/提示用。</summary>
    public static string Format(int tokens)
        => tokens >= 1000 ? $"{tokens / 1000.0:0.#}k" : tokens.ToString();

    /// <summary>把一段文本压成单行摘要（估算日志里用）。</summary>
    public static string Preview(string? text, int max = 60)
    {
        var line = (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        var builder = new StringBuilder(line.Length);
        var lastWasSpace = false;
        foreach (var ch in line)
        {
            if (ch == ' ')
            {
                if (lastWasSpace)
                {
                    continue;
                }

                lastWasSpace = true;
            }
            else
            {
                lastWasSpace = false;
            }

            builder.Append(ch);
        }

        return builder.Length <= max ? builder.ToString() : builder.ToString(0, max) + "…";
    }
}
