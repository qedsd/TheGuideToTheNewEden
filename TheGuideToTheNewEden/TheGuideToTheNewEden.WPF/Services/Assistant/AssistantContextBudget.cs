using TheGuideToTheNewEden.WPF.Services.Translation.Llm;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>
/// 一次请求的**上下文预算**（阶段 134）：从模型窗口里扣掉系统提示词、工具声明、输出预留、
/// 安全余量与"本轮工具结果预留"，剩下的才是可以给历史记录的 token。
/// <para>
/// 这是 P0 的核心：阶段 132 的预算是**字符**且只算历史，既不知道工具声明的 2 万多字符，
/// 也不知道本轮工具往返还要占多少 —— 对 1M 窗口的 DeepSeek 尚可，对 128k/200k 窗口的模型
/// （尤其中文历史）会直接超窗。现在统一折算成 token 并把这些都扣掉，
/// 界面底栏显示的也是这份预算，而不是"历史字符 / 某个凭感觉定的上限"。
/// </para>
/// <para>
/// **预算 0 = 不限制**：用于接本地小窗口模型时手动放宽（风险由用户承担，界面会标明"不限"）。
/// 安全余量 = 窗口的 5%（至少 512 token），用来吸收估算误差与协议包装。
/// </para>
/// </summary>
public sealed record AssistantContextBudget(
    int WindowTokens,
    int SystemTokens,
    int ToolTokens,
    int OutputReserveTokens,
    int SafetyMarginTokens,
    int CurrentTurnReserveTokens,
    int AvailableHistoryTokens,
    bool Unlimited)
{
    /// <summary>安全余量比例（窗口的 5%）。</summary>
    public const int SafetyMarginPercent = 5;

    /// <summary>安全余量下限（token）。</summary>
    public const int MinSafetyMarginTokens = 512;

    /// <summary>本轮工具结果预留里给"用户提问本身"的部分（token）。</summary>
    public const int CurrentTurnQuestionTokens = 1024;

    /// <summary>
    /// 生成预算。
    /// </summary>
    /// <param name="windowTokens">模型上下文窗口（token）；<c>&lt;= 0</c> = 不限制。</param>
    /// <param name="systemPrompt">实际下发的系统提示词（自定义或内置）。</param>
    /// <param name="tools">可能被声明的工具（预算按"全部已启用"算，是上限）。</param>
    /// <param name="outputReserveTokens">输出预留（取设置与 <c>Ai.MaxTokens</c> 的较大者）。</param>
    /// <param name="maxTurnToolTokens">单轮工具结果上限（作为本轮预留）。</param>
    public static AssistantContextBudget Create(
        int windowTokens,
        string? systemPrompt,
        IReadOnlyList<ChatTool>? tools,
        int outputReserveTokens,
        int maxTurnToolTokens)
    {
        var system = AssistantTokenEstimator.Estimate(systemPrompt);
        var toolTokens = AssistantTokenEstimator.EstimateTools(tools);
        var output = Math.Max(0, outputReserveTokens);
        var currentTurn = Math.Max(0, maxTurnToolTokens) + CurrentTurnQuestionTokens;

        if (windowTokens <= 0)
        {
            return new AssistantContextBudget(
                0, system, toolTokens, output, 0, currentTurn, 0, Unlimited: true);
        }

        var margin = Math.Max(MinSafetyMarginTokens, windowTokens * SafetyMarginPercent / 100);
        var available = Math.Max(0, windowTokens - system - toolTokens - output - margin - currentTurn);

        return new AssistantContextBudget(
            windowTokens,
            system,
            toolTokens,
            output,
            margin,
            currentTurn,
            available,
            Unlimited: false);
    }

    /// <summary>本次请求允许的输入上限（窗口 − 输出预留 − 安全余量；不含本轮预留）。</summary>
    public int AllowedInputTokens => Unlimited
        ? int.MaxValue
        : Math.Max(0, WindowTokens - OutputReserveTokens - SafetyMarginTokens);

    /// <summary>固定前缀（系统提示词 + 工具声明）的估算 token。</summary>
    public int FixedTokens => SystemTokens + ToolTokens;

    /// <summary>一句话说明（日志与界面提示用）。</summary>
    public string Describe()
        => Unlimited
            ? "上下文预算：不限"
            : $"上下文预算：可用历史 {AssistantTokenEstimator.Format(AvailableHistoryTokens)} token"
              + $"（窗口 {AssistantTokenEstimator.Format(WindowTokens)}"
              + $" − 提示词 {AssistantTokenEstimator.Format(SystemTokens)}"
              + $" − 工具 {AssistantTokenEstimator.Format(ToolTokens)}"
              + $" − 输出 {AssistantTokenEstimator.Format(OutputReserveTokens)}"
              + $" − 余量 {AssistantTokenEstimator.Format(SafetyMarginTokens)}"
              + $" − 本轮 {AssistantTokenEstimator.Format(CurrentTurnReserveTokens)}）";
}
