using TheGuideToTheNewEden.WPF.Services.Translation.Llm;

namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>AI 翻译（大模型）的全部设置项。</summary>
public sealed class AiTranslationSettings
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

    /// <summary>最大输出 token；0 表示不下发。</summary>
    public int MaxTokens { get; set; } = 1024;

    /// <summary>单次请求超时（秒）。</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>是否注入术语表（SDE 中英词库）。</summary>
    public bool UseGlossary { get; set; } = true;

    /// <summary>单次注入的术语条数上限（越多越准也越贵，30 条约 300–600 token）。</summary>
    public int GlossaryLimit { get; set; } = 30;

    /// <summary>是否流式输出（界面可实时看到生成进度）。</summary>
    public bool Stream { get; set; } = true;

    /// <summary>是否启用结果缓存（同模型/提示词/术语库/原文直接复用）。</summary>
    public bool Cache { get; set; } = true;

    /// <summary>是否把"术语库没有、又没原样保留"的名词记进"AI 新词候选"（供人工确认）。</summary>
    public bool CollectCandidates { get; set; } = true;

    /// <summary>
    /// 单次请求最多带多少字符（超过就分片，分片结果按顺序拼回；**0 = 不分片**，整段一次发）。
    /// <para>
    /// 默认 12000：约 3000–6000 token，对现代模型（上下文动辄 128K–1M）来说很小，
    /// 但能让"首段结果更快出现"、单次失败不至于全丢。真正的上限是模型的**输出**上限，见 <see cref="ThinkingMode"/> 上方的说明与
    /// "输出被截断会自动续写"的保护（`AiTranslationProvider`）。
    /// </para>
    /// </summary>
    public int MaxChunkChars { get; set; } = 12000;

    /// <summary>
    /// 思考模式（DeepSeek 系模型专有，见 <see cref="ThinkingModes"/>）：
    /// <c>auto</c> = 跟服务端默认；<c>off</c>/<c>low</c>/<c>high</c> = 关闭 / 低 / 高。
    /// 翻译这种任务基本不需要深度思考，而 DeepSeek 默认是 <c>high</c>（又慢又贵、且会让 temperature 失效），
    /// 所以默认 <c>off</c>。
    /// </summary>
    public string ThinkingMode { get; set; } = ThinkingModes.Off;

    /// <summary>自定义系统提示词；留空使用 <see cref="AiTranslationProvider.DefaultSystemPrompt"/>。</summary>
    public string SystemPrompt { get; set; } = string.Empty;

    public AiTranslationSettings Clone() => new()
    {
        Protocol = Protocol,
        BaseUrl = BaseUrl,
        ApiKey = ApiKey,
        Model = Model,
        ApiVersion = ApiVersion,
        Temperature = Temperature,
        MaxTokens = MaxTokens,
        TimeoutSeconds = TimeoutSeconds,
        UseGlossary = UseGlossary,
        GlossaryLimit = GlossaryLimit,
        Stream = Stream,
        Cache = Cache,
        CollectCandidates = CollectCandidates,
        MaxChunkChars = MaxChunkChars,
        ThinkingMode = ThinkingMode,
        SystemPrompt = SystemPrompt,
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
