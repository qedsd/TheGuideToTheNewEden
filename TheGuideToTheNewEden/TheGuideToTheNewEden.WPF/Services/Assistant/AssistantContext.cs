using TheGuideToTheNewEden.WPF.Services.Translation.Llm;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>一次"发一条消息"的上下文（引擎 SendAsync 的入参）。</summary>
public sealed class AssistantContext
{
    /// <summary>
    /// 对话级"带上上下文"：本对话此前**成功**轮次重建出来的完整消息序列
    /// （<c>user(提问)</c> → <c>assistant(tool_calls)</c> → <c>tool(结果)</c> … → <c>assistant(回答)</c>，
    /// 由 <see cref="AssistantContextBuilder"/> 生成；引擎按顺序插到 system 之后、本次提问之前）。
    /// <para>
    /// 阶段 132 起**含工具往返**（此前只有"问 / 答"两段文本）。预算由界面按
    /// <c>Assistant.ContextWindowTokens</c> 推导成 token 后传入（见 <see cref="AssistantContextBudget"/>）：
    /// 超出时优先摘要压缩较早轮次，压不动才整轮省略。
    /// **记忆只存在于单个对话**，不同对话之间不共享。
    /// </para>
    /// </summary>
    public IReadOnlyList<ChatMessage>? Context { get; init; }

    /// <summary>工具执行上下文（当前角色 / UI 线程归位 / 进度回调）。</summary>
    public required ToolContext Tools { get; init; }

    /// <summary>自定义系统提示词（null = 内置 <see cref="AiAssistantEngine.DefaultSystemPrompt"/>）。</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// 追加在系统提示词末尾的内容（阶段 134）：会话摘要 + "中间轮次已省略"的说明。
    /// 由引擎拼到实际下发的系统提示词后面（无论用的是自定义还是内置提示词）；
    /// 不插进消息序列——Anthropic 等协议不接受中途的 system 角色消息。
    /// </summary>
    public string? SystemSuffix { get; init; }

    /// <summary>
    /// 本对话（更早的轮次）**已经用过**的工具名——按需声明路由的"粘性"输入（阶段 123）：
    /// 用过的工具在后续请求里始终保留声明，避免"上一轮能用、下一轮突然没了"。
    /// 由界面从当前对话的历史记录里取（<c>AiAssistantViewModel</c>）。
    /// </summary>
    public IReadOnlySet<string>? StickyTools { get; init; }

    /// <summary>
    /// 本次使用的模型端点；<c>null</c> = 用「AI 翻译」的设置（<c>TranslationSettingService.AiEndpoint</c>）。
    /// 留这个口子是为了**可测**：探针可以指向本地 mock 服务，而不用动用户的 settings.json。
    /// </summary>
    public ChatEndpoint? Endpoint { get; init; }
}
