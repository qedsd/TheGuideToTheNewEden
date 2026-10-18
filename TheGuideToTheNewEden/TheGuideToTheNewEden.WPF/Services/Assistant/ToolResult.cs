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

/// <summary>
/// 一次工具执行记录（界面"工具步骤区"的数据源，同时是**落盘历史**的来源）。
/// <para>
/// 阶段 132 起后四个字段是"重建上下文"所必需的：同一条 assistant(tool_calls) 里的多个调用靠
/// <paramref name="Round"/> 归组，回填用的 tool 消息靠 <paramref name="CallId"/> 配对，
/// <paramref name="ArgumentsJson"/> / <paramref name="ResultText"/> 决定重放给模型的内容。
/// 前五个参数是原有形状（界面显示用），保持位置不变以免改动既有调用点。
/// </para>
/// </summary>
/// <param name="Round">工具轮号（同一轮 = 同一条 assistant 消息里的多个调用）。</param>
/// <param name="CallId">工具调用 ID（tool 消息配对用）。</param>
/// <param name="ArgumentsJson">调用参数 JSON 原文。</param>
/// <param name="ResultText">回填给模型的结果全文（已按引擎上限截断）。</param>
public sealed record ToolStep(
    string Name,
    string ParamSummary,
    string ResultSummary,
    bool Success,
    double ElapsedSeconds,
    int Round = 0,
    string? CallId = null,
    string? ArgumentsJson = null,
    string? ResultText = null);

/// <summary>
/// 挂起的待确认动作（引擎命中 <see cref="ITool.RequiresConfirmation"/> 工具时生成；
/// 用户确认后经 <see cref="AiAssistantEngine.Turn.ResumeAsync"/> 执行）。
/// </summary>
public sealed record PendingToolAction(string Id, string ToolName, string ParamSummary, string ArgumentsJson);
