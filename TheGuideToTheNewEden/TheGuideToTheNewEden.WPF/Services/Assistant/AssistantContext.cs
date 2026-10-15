using TheGuideToTheNewEden.WPF.Services.Translation.Llm;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>一次"发一条消息"的上下文（引擎 SendAsync 的入参）。</summary>
public sealed class AssistantContext
{
    /// <summary>
    /// 对话级"带上上下文"：此前**成功**轮次的问答对（引擎拼进消息开头，按问答对交替；
    /// 只含问/答，不含工具内部往返，防止消息膨胀）。
    /// </summary>
    public IReadOnlyList<(string Question, string Answer)>? Context { get; init; }

    /// <summary>工具执行上下文（当前角色 / UI 线程归位 / 进度回调）。</summary>
    public required ToolContext Tools { get; init; }

    /// <summary>自定义系统提示词（null = 内置 <see cref="AiAssistantEngine.DefaultSystemPrompt"/>）。</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// 本次使用的模型端点；<c>null</c> = 用「AI 翻译」的设置（<c>TranslationSettingService.AiEndpoint</c>）。
    /// 留这个口子是为了**可测**：探针可以指向本地 mock 服务，而不用动用户的 settings.json。
    /// </summary>
    public ChatEndpoint? Endpoint { get; init; }
}
