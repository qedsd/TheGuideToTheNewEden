using System.Text.Json;
using System.Text.Json.Nodes;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>
/// 一个可被 AI 助手调用的工具（查询 / 动作 / 写）。
/// <para>
/// 执行约定：查询类工具可直接在调用线程执行（服务层是线程安全的静态类/单例，统一 try/catch → null）；
/// 导航等动作必须经 <see cref="ToolContext.OnUiThread"/> 归位 UI 线程。
/// 返回给模型的 <see cref="ToolResult.Text"/> 要紧凑（控制 token）；失败时给"原因"而不是空串。
/// </para>
/// </summary>
public interface ITool
{
    /// <summary>英文工具名（面向 LLM，如 <c>market_orders</c>）。</summary>
    string Name { get; }

    /// <summary>界面中文名的本地化键（<c>AssistantTool_&lt;name&gt;</c>）。</summary>
    string DisplayNameKey { get; }

    /// <summary>LLM 看的描述（英文）：何时用、参数、返回、已知限制。</summary>
    string Description { get; }

    /// <summary>参数 JSON Schema（object 型）；null = 无参数。</summary>
    JsonObject? Parameters { get; }

    /// <summary>
    /// 写操作标记：引擎命中此工具时**不执行**，挂起返回 <see cref="PendingToolAction"/>，
    /// 由界面显示确认卡片、用户确认后经 <see cref="AiAssistantEngine.Turn.ResumeAsync"/> 执行。
    /// </summary>
    bool RequiresConfirmation { get; }

    /// <summary>动作类（导航跳转等）：结果带 <see cref="ToolResult.NavigateAction"/>。</summary>
    bool IsAction { get; }

    /// <summary>执行工具。参数是模型给出的 JSON 参数（引擎已解析成 <see cref="JsonElement"/>）。</summary>
    Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct);
}

/// <summary>
/// 可选接口：让写工具自己写"待确认动作"的人话摘要。
/// <para>
/// 引擎默认用截断后的**原始参数 JSON** 当摘要，对 <c>esi_write</c> 这种
/// "operation + path + body" 结构的参数几乎不可读（用户看不懂确认卡片）。
/// 实现本接口后，确认卡片显示的就是实现方给出的一句话（如
/// <c>POST /characters/{character_id}/mail：收件人 123,456；主题 你好</c>）。
/// </para>
/// </summary>
public interface IPendingSummary
{
    /// <summary>给用户看的动作摘要（<paramref name="args"/> 是模型给出的参数）。</summary>
    string DescribePending(JsonElement args);
}
