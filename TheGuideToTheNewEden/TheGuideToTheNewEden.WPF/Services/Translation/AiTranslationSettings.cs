namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>
/// AI **翻译专属**设置（阶段 125 起只留这一半）：术语库注入、单次请求分块、结果缓存、翻译系统提示词。
/// <para>
/// 模型端点与生成参数（协议 / 地址 / Key / 模型 / 温度 / 最大输出 token / 超时 / 思考模式 / 流式）
/// 已拆到 <see cref="Llm.AiModelSettings"/> —— 那是助手与翻译**共用**的一份，
/// 存储在 <c>Ai.*</c> 键、由 <c>AiModelSettingService</c> 负责读写。
/// </para>
/// </summary>
public sealed class AiTranslationSettings
{
    /// <summary>是否注入术语表（SDE 中英词库）。</summary>
    public bool UseGlossary { get; set; } = true;

    /// <summary>单次注入的术语条数上限（越多越准也越贵，30 条约 300–600 token）。</summary>
    public int GlossaryLimit { get; set; } = 30;

    /// <summary>是否启用结果缓存（同模型/提示词/术语库/原文直接复用）。</summary>
    public bool Cache { get; set; } = true;

    /// <summary>是否把"术语库没有、又没原样保留"的名词记进"AI 新词候选"（供人工确认）。</summary>
    public bool CollectCandidates { get; set; } = true;

    /// <summary>
    /// 单次请求最多带多少字符（超过就分片，分片结果按顺序拼回；**0 = 不分片**，整段一次发）。
    /// <para>
    /// 默认 12000：约 3000–6000 token，对现代模型（上下文动辄 128K–1M）来说很小，
    /// 但能让"首段结果更快出现"、单次失败不至于全丢。真正的上限是模型的**输出**上限，
    /// 见"输出被截断会自动续写"的保护（<c>AiTranslationProvider</c>）。
    /// </para>
    /// </summary>
    public int MaxChunkChars { get; set; } = 12000;

    /// <summary>翻译用的自定义系统提示词；留空使用 <c>AiTranslationProvider.DefaultSystemPrompt</c>。</summary>
    public string SystemPrompt { get; set; } = string.Empty;

    public AiTranslationSettings Clone() => new()
    {
        UseGlossary = UseGlossary,
        GlossaryLimit = GlossaryLimit,
        Cache = Cache,
        CollectCandidates = CollectCandidates,
        MaxChunkChars = MaxChunkChars,
        SystemPrompt = SystemPrompt,
    };
}
