namespace TheGuideToTheNewEden.WPF.Services.Translation.Llm;

/// <summary>
/// 思考模式的取值（DeepSeek 系模型专有，OpenAI 格式下是请求体里的
/// <c>{"thinking": {"type": "enabled/disabled"}}</c> 加 <c>reasoning_effort</c>）。
/// <para>
/// 为什么要暴露这个：DeepSeek 的思考模式**默认开启且 effort = high**，翻一句"禁止刷屏"也要先写一大段思维链，
/// 既慢又贵；而且在思考模式下 <c>temperature</c> 是失效的。翻译场景默认关掉更合适。
/// 详见 <see href="https://api-docs.deepseek.com/guides/thinking_mode"/>。
/// </para>
/// </summary>
public static class ThinkingModes
{
    /// <summary>不下发任何字段，跟服务端默认（DeepSeek 下即"开启 + high"）。</summary>
    public const string Auto = "auto";

    /// <summary>关闭思考：<c>thinking.type = disabled</c>。</summary>
    public const string Off = "off";

    /// <summary>开启思考但压低强度：<c>thinking.type = enabled</c> + <c>reasoning_effort = low</c>。</summary>
    public const string Low = "low";

    /// <summary>开启思考并高强度：<c>thinking.type = enabled</c> + <c>reasoning_effort = high</c>。</summary>
    public const string High = "high";

    /// <summary>全部取值（设置页下拉与合法性校验用）。</summary>
    public static readonly string[] All = [Auto, Off, Low, High];

    /// <summary>是否是一个认识的取值。</summary>
    public static bool IsDefined(string? value)
        => value is not null && Array.IndexOf(All, value) >= 0;
}
