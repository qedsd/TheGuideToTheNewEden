namespace TheGuideToTheNewEden.WPF.Services.Translation.Llm;

/// <summary>
/// 大模型**端点与生成参数**（阶段 125 从 <c>AiTranslationSettings</c> 拆出）：
/// 这是"怎么调用模型"的那一半，**AI 助手与 AI 翻译共用同一份**（设置 → AI → 模型）。
/// 翻译专属的那一半（术语库 / 分块 / 结果缓存 / 翻译提示词）见
/// <see cref="AiTranslationSettings"/>。
/// </summary>
public sealed class AiModelSettings
{
    /// <summary>协议标识，见 <see cref="ChatProtocolFactory"/>。</summary>
    public string Protocol { get; set; } = ChatProtocolFactory.OpenAi;

    /// <summary>服务地址：可填裸域名、带版本路径的地址，或完整的 chat/completions 地址。</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>模型名（Azure OpenAI 下填部署名）。</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Azure OpenAI 的 api-version（其他协议忽略）。</summary>
    public string ApiVersion { get; set; } = string.Empty;

    /// <summary>采样温度；0 表示不下发（给不接受该参数的推理模型留出口）。</summary>
    public double Temperature { get; set; } = 0.2;

    /// <summary>
    /// 最大输出 token；<b>0 = 不下发</b>（交给服务端默认），这是阶段 121 起的默认值。
    /// <remarks>
    /// 原先默认 1024：助手一次请求要带 40+ 个工具声明（输入上万 token），1024 的输出上限
    /// 很容易把**工具调用 JSON 或 Markdown 答案**截断（DeepSeek 的 DSML 标记泄漏也与截断叠加过，
    /// 见 REFACTORING 阶段 120）。现在默认交给服务端——它们的默认上限通常远大于 1024。
    /// </remarks>
    /// </summary>
    public int MaxTokens { get; set; } = 0;

    /// <summary>单次请求超时（秒）。</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>是否流式输出（界面可实时看到生成进度）。</summary>
    public bool Stream { get; set; } = true;

    /// <summary>
    /// 思考模式（DeepSeek 系模型专有，见 <see cref="ThinkingModes"/>）：
    /// <c>auto</c> = 跟服务端默认；<c>off</c>/<c>low</c>/<c>high</c> = 关闭 / 低 / 高。
    /// 助手与翻译这类任务基本不需要深度思考，而 DeepSeek 默认是 <c>high</c>（又慢又贵、且会让 temperature 失效），
    /// 所以默认 <c>off</c>。
    /// </summary>
    public string ThinkingMode { get; set; } = ThinkingModes.Off;

    public AiModelSettings Clone() => new()
    {
        Protocol = Protocol,
        BaseUrl = BaseUrl,
        ApiKey = ApiKey,
        Model = Model,
        ApiVersion = ApiVersion,
        Temperature = Temperature,
        MaxTokens = MaxTokens,
        TimeoutSeconds = TimeoutSeconds,
        Stream = Stream,
        ThinkingMode = ThinkingMode,
    };

    /// <summary>转成协议层用的端点描述。</summary>
    public ChatEndpoint ToEndpoint() => new()
    {
        Protocol = Protocol,
        BaseUrl = BaseUrl,
        ApiKey = ApiKey,
        Model = Model,
        ApiVersion = ApiVersion,
        Temperature = Temperature,
        MaxTokens = MaxTokens,
        TimeoutSeconds = Math.Clamp(TimeoutSeconds <= 0 ? 60 : TimeoutSeconds, 5, 600),
        ThinkingMode = ResolveThinkingMode(),
    };

    /// <summary>
    /// 决定这一次请求到底要不要带思考模式参数：**只有 DeepSeek 系端点才带**。
    /// 别的服务商（OpenAI/Azure/Anthropic/Gemini 以及各类网关）不认识 <c>thinking</c> 字段，
    /// 硬发过去可能直接 400，所以这里按"模型名或地址里含 deepseek"来判断，其余一律退回 <c>auto</c>。
    /// </summary>
    private string ResolveThinkingMode()
    {
        if (string.IsNullOrWhiteSpace(ThinkingMode) || ThinkingMode == ThinkingModes.Auto)
        {
            return ThinkingModes.Auto;
        }

        var looksLikeDeepSeek = (Model?.Contains("deepseek", StringComparison.OrdinalIgnoreCase) ?? false)
            || (BaseUrl?.Contains("deepseek", StringComparison.OrdinalIgnoreCase) ?? false);

        return looksLikeDeepSeek ? ThinkingMode : ThinkingModes.Auto;
    }
}
