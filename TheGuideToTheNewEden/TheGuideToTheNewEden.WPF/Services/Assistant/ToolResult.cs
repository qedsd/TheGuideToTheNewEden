namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>一次工具执行的结果。</summary>
/// <param name="Text">给模型看的紧凑文本（控制 token；失败时给"原因"而不是空串）。</param>
/// <param name="DisplayText">给用户看的文本（缺省用 <paramref name="Text"/>）。</param>
/// <param name="NavigateAction">动作（引擎经 UI 线程执行）；IsAction 工具用。</param>
/// <param name="ParamSummary">人话参数摘要（如 "巨神兵级 @ 伏尔戈"）；缺省由引擎截断参数 JSON 兜底。</param>
public sealed record ToolResult(
    string Text,
    string? DisplayText = null,
    Action? NavigateAction = null,
    string? ParamSummary = null);

/// <summary>一次工具执行记录（界面"工具步骤区"的数据源）。</summary>
public sealed record ToolStep(string Name, string ParamSummary, string ResultSummary, bool Success, double ElapsedSeconds);

/// <summary>
/// 挂起的待确认动作（引擎命中 <see cref="ITool.RequiresConfirmation"/> 工具时生成；
/// 用户确认后经 <see cref="AiAssistantEngine.Turn.ResumeAsync"/> 执行）。
/// </summary>
public sealed record PendingToolAction(string Id, string ToolName, string ParamSummary, string ArgumentsJson);
