using System.Diagnostics;
using System.Text;
using TheGuideToTheNewEden.WPF.Services.Translation.Llm;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>一次上下文压缩的结果。</summary>
/// <param name="Summary">压缩后的摘要（中文短句/列表）。</param>
/// <param name="UpToTurnId">摘要已覆盖到的最后一条轮次 Id（该轮及更早的不再原样带上）。</param>
/// <param name="SummarizedTurns">本次新折进摘要的轮次数。</param>
/// <param name="Usage">压缩调用累计的 token 用量（用于日志/成本感知）。</param>
public sealed record AssistantCompactionResult(
    string Summary,
    Guid UpToTurnId,
    int SummarizedTurns,
    ChatUsage Usage);

/// <summary>
/// 摘要式上下文压缩（阶段 134 / P2）：当历史超出 token 预算时，把**较早的轮次**交给模型压成一份事实摘要，
/// 后续请求只带"摘要 + 最近几轮"，而不是把中间轮次整轮丢弃。
/// <para>
/// 为什么需要它：阶段 132 的滑窗是"整轮丢弃"，丢掉的中间事实模型再也看不见，追问时只能重新查一次；
/// 长对话里还会出现"它明明查过却说不知道"。摘要把"原始目标 + 关键数字/ID + 未完成动作"留下来，
/// 丢掉寒暄与过程，是长会话里唯一既能省 token 又不失忆的做法。
/// </para>
/// <para>
/// **增量压缩**：<see cref="AiAssistantSession.ContextSummary"/> + <c>SummaryUpToTurnId</c> 落盘，
/// 下次只压"上次摘要之后新变旧"的轮次，不会每次重压全史；压完的轮次不再原样发送。
/// **分批**：每批转写不超过 <see cref="BatchTokens"/>，避免"压上下文"的请求自己又超窗（map-reduce）。
/// **失败可降级**：任何一批失败都返回 null，调用方退回滑窗（不会阻塞用户提问），旧摘要保持不变。
/// </para>
/// <para>纯服务层：端点由调用方注入（探针可指向 mock），不直接读设置、不碰界面。</para>
/// </summary>
public static class AssistantContextCompactor
{
    /// <summary>摘要正文的最大字符数（≈ 500–800 token）。</summary>
    public const int MaxSummaryChars = 1600;

    /// <summary>单批转写的 token 上限（压缩请求自身的输入预算）。</summary>
    public const int BatchTokens = 6000;

    /// <summary>转写里每条工具结果保留的字符数（摘要只需要事实，不需要整张表）。</summary>
    private const int TranscriptResultChars = 300;

    /// <summary>摘要调用的输出上限（token）。</summary>
    private const int SummaryMaxTokens = 1200;

    /// <summary>至少保留最近几轮不压（最近的两轮通常是用户当前在追问的内容）。</summary>
    public const int MinRecentTurns = 2;

    private const string SummarySystemPrompt =
        """
        你是《新伊甸漫游指南》AI 助手的对话压缩器。把给定的对话片段压缩成供后续轮次直接使用的事实摘要。

        必须保留：
        - 用户的原始目标与约束（要什么、不要什么、角色/市场/星域/时间范围等限定）
        - 已查到并确认的事实：价格、数量、ID、名称、时间、状态（数字与单位原样保留）
        - 仍未完成的动作、待用户确认的写操作、用户拒绝过的操作
        - 用户纠正过的点，以及明确表示不满意或不要再做的事

        可以丢弃：寒暄、重复解释、已被后续数据推翻的中间过程、工具调用的格式细节。

        规则：
        1. 只依据给定片段，不要补全、不要推测、不要编造数字。
        2. 用中文、短句或列表，总长不超过 1200 字。
        3. 数字与 ID 不要改写、不要四舍五入。
        4. 如果片段里给了"上一条摘要"，把它与新片段合并成一份摘要；不要出现"上次摘要说"这类套话。
        5. **新片段优先于旧摘要**：新片段里用户改口、否定、纠正、换市场/角色/范围，或拒绝过某个操作时，
           一律覆盖旧摘要中的对应内容并删除已被推翻的旧目标；不要把已废弃的意图继续保留。
        """;

    /// <summary>
    /// 判断是否需要压缩并执行压缩。
    /// </summary>
    /// <param name="turns">该对话的全部轮次（时间顺序，含 <paramref name="current"/>）。</param>
    /// <param name="current">本次提问对应的轮次（不当作历史）。</param>
    /// <param name="existingSummary">上一次的摘要（null/空 = 没有）。</param>
    /// <param name="existingUpToTurnId">上次摘要覆盖到的轮次 Id。</param>
    /// <param name="availableTokens">历史可用的 token 预算（见 <see cref="AssistantContextBudget"/>）。</param>
    /// <param name="endpoint">模型端点（探针可注入 mock）。</param>
    /// <param name="progress">压缩进度回调（已完成批数 / 总批数）。</param>
    public static async Task<AssistantCompactionResult?> CompactAsync(
        IReadOnlyList<AiAssistantTurn> turns,
        AiAssistantTurn? current,
        string? existingSummary,
        Guid? existingUpToTurnId,
        int availableTokens,
        ChatEndpoint endpoint,
        CancellationToken ct,
        Action<int, int>? progress = null)
    {
        if (turns.Count == 0 || availableTokens <= 0 || !endpoint.IsConfigured)
        {
            return null;
        }

        // ① 上次摘要覆盖到哪一轮；找不到（被 200 轮上限裁掉/清空）就从头开始，旧摘要作废
        var start = 0;
        var summary = existingSummary?.Trim();
        if (!string.IsNullOrEmpty(summary) && existingUpToTurnId is { } coveredId)
        {
            var index = FindIndex(turns, coveredId);
            if (index < 0)
            {
                summary = null;
            }
            else
            {
                start = index + 1;
            }
        }
        else
        {
            summary = null;
        }

        // ② 候选 = 摘要之后、当前提问之前的成功轮次
        var candidates = new List<AiAssistantTurn>();
        for (var i = start; i < turns.Count; i++)
        {
            var turn = turns[i];
            if (ReferenceEquals(turn, current))
            {
                break;
            }

            if (AssistantContextBuilder.CanBeContext(turn))
            {
                candidates.Add(turn);
            }
        }

        // 轮次太少没有压缩价值（最近两轮要原样保留）
        if (candidates.Count <= MinRecentTurns)
        {
            return null;
        }

        // ③ 不超预算就不压（避免每次提问都花一次调用）
        var tokens = candidates.Sum(turn => AssistantContextBuilder.EstimateTurnTokens(turn));
        var summaryTokens = AssistantTokenEstimator.Estimate(summary);
        if (tokens + summaryTokens <= availableTokens * 9 / 10)
        {
            return null;
        }

        // ④ 保留最近几轮：从最新往回填，最多占"摘要之外的一半预算"
        var recentBudget = Math.Max(1500, (availableTokens - MaxSummaryChars) / 2);
        var keep = 0;
        var recentTokens = 0;
        for (var i = candidates.Count - 1; i >= 0 && keep < candidates.Count - 1; i--)
        {
            var cost = AssistantContextBuilder.EstimateTurnTokens(candidates[i]);
            if (keep >= MinRecentTurns && recentTokens + cost > recentBudget)
            {
                break;
            }

            keep++;
            recentTokens += cost;
        }

        keep = Math.Max(MinRecentTurns, keep);
        keep = Math.Min(keep, candidates.Count - 1);   // 至少要折一轮进去，否则没有进展
        var foldCount = candidates.Count - keep;
        if (foldCount <= 0)
        {
            return null;
        }

        var fold = candidates.GetRange(0, foldCount);
        var started = Stopwatch.StartNew();
        try
        {
            var compacted = await SummarizeAsync(fold, summary, endpoint, ct, progress).ConfigureAwait(false);
            if (compacted is null)
            {
                return null;
            }

            started.Stop();
            Core.Log.Info(
                $"AI 助手 · 上下文压缩：{foldCount} 轮 → {compacted.Text.Length} 字"
                + $" · {started.Elapsed.TotalSeconds:F1}s"
                + $" · 输入 {compacted.Usage.InputTokens} / 输出 {compacted.Usage.OutputTokens} token");

            return new AssistantCompactionResult(
                compacted.Text,
                fold[^1].Id,
                foldCount,
                compacted.Usage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            Core.Log.Info($"AI 助手 · 上下文压缩失败（退回滑窗，不阻塞本次提问）：{ex.Message}");
            return null;
        }
    }

    /// <summary>把一段轮次压成摘要（可能分多批 map-reduce）。失败返回 null。</summary>
    private static async Task<Compacted?> SummarizeAsync(
        IReadOnlyList<AiAssistantTurn> turns,
        string? previousSummary,
        ChatEndpoint endpoint,
        CancellationToken ct,
        Action<int, int>? progress)
    {
        var batches = BuildBatches(turns);
        if (batches.Count == 0)
        {
            return null;
        }

        var summary = previousSummary?.Trim();
        var usage = ChatUsage.Empty;

        for (var i = 0; i < batches.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Invoke(i + 1, batches.Count);

            var user = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(summary))
            {
                user.Append("上一条摘要：\n").Append(summary).Append("\n\n");
            }

            user.Append("新的对话片段：\n").Append(batches[i]);

            var completion = await ChatClient.CompleteAsync(
                new ChatRequest
                {
                    Endpoint = SummaryEndpoint(endpoint),
                    SystemPrompt = SummarySystemPrompt,
                    Messages = [ChatMessage.User(user.ToString())],
                    Stream = false,
                    Tools = null,
                },
                ct).ConfigureAwait(false);

            usage = new ChatUsage(
                usage.InputTokens + completion.Usage.InputTokens,
                usage.OutputTokens + completion.Usage.OutputTokens)
            {
                PromptCacheHitTokens = usage.PromptCacheHitTokens + completion.Usage.PromptCacheHitTokens,
            };

            var text = (completion.Text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                Core.Log.Warn("AI 助手 · 上下文压缩：模型返回空摘要，放弃本次压缩");
                return null;
            }

            summary = text.Length <= MaxSummaryChars
                ? text
                : text[..MaxSummaryChars] + "…（摘要已截断）";
        }

        return summary is null ? null : new Compacted(summary, usage);
    }

    private sealed record Compacted(string Text, ChatUsage Usage);

    /// <summary>把轮次转写成文本并按 <see cref="BatchTokens"/> 分批（单轮过大时再按字符切片）。</summary>
    private static List<string> BuildBatches(IReadOnlyList<AiAssistantTurn> turns)
    {
        var batches = new List<string>();
        var current = new StringBuilder();
        var currentTokens = 0;

        foreach (var turn in turns)
        {
            foreach (var part in SplitTranscript(Transcript(turn)))
            {
                var cost = AssistantTokenEstimator.Estimate(part);
                if (current.Length > 0 && currentTokens + cost > BatchTokens)
                {
                    batches.Add(current.ToString().TrimEnd());
                    current.Clear();
                    currentTokens = 0;
                }

                current.Append(part).Append("\n\n");
                currentTokens += cost;
            }
        }

        if (current.Length > 0)
        {
            batches.Add(current.ToString().TrimEnd());
        }

        return batches;
    }

    /// <summary>
    /// 单轮转写过大时按 <see cref="BatchTokens"/> 字符切片（6000 字符即使全中文也约 5100 token，
    /// 不会让某一片自己超预算）。切片优先落在换行处。
    /// </summary>
    private static IEnumerable<string> SplitTranscript(string text)
    {
        if (text.Length <= BatchTokens)
        {
            yield return text;
            yield break;
        }

        var offset = 0;
        while (offset < text.Length)
        {
            var length = Math.Min(BatchTokens, text.Length - offset);
            if (offset + length < text.Length)
            {
                var lastBreak = text.LastIndexOf('\n', offset + length - 1, length);
                if (lastBreak > offset + BatchTokens / 2)
                {
                    length = lastBreak - offset + 1;
                }
            }

            yield return text.Substring(offset, length);
            offset += length;
        }
    }

    /// <summary>一轮的可压缩转写：提问 + 每条工具结果的前 300 字 + 回答。</summary>
    private static string Transcript(AiAssistantTurn turn)
    {
        var builder = new StringBuilder();
        builder.Append("【轮次 · ").Append(turn.Time.ToString("MM-dd HH:mm")).Append("】\n");
        builder.Append("用户：").Append(ToolArgs.Truncate(turn.UserText, 800)).Append('\n');

        foreach (var step in turn.Steps ?? [])
        {
            if (string.IsNullOrWhiteSpace(step.ToolName))
            {
                continue;
            }

            var result = !string.IsNullOrWhiteSpace(step.ResultText) ? step.ResultText : step.ResultSummary;
            builder.Append("工具 ").Append(step.ToolName).Append("：")
                .Append(ToolArgs.Truncate(result, TranscriptResultChars)).Append('\n');
        }

        builder.Append("助手：").Append(ToolArgs.Truncate(turn.AnswerText, 1000)).Append('\n');
        return builder.ToString();
    }

    /// <summary>
    /// 压缩调用专用的端点：强制非流式、低温、输出上限 1200；
    /// 只有 DeepSeek 系才下发"关闭思考"（其它服务商不认识该字段，硬发可能 400）。
    /// </summary>
    private static ChatEndpoint SummaryEndpoint(ChatEndpoint endpoint)
    {
        var looksLikeDeepSeek = (endpoint.Model?.Contains("deepseek", StringComparison.OrdinalIgnoreCase) ?? false)
            || (endpoint.BaseUrl?.Contains("deepseek", StringComparison.OrdinalIgnoreCase) ?? false);

        return new ChatEndpoint
        {
            Protocol = endpoint.Protocol ?? ChatProtocolFactory.OpenAi,
            BaseUrl = endpoint.BaseUrl ?? string.Empty,
            ApiKey = endpoint.ApiKey ?? string.Empty,
            Model = endpoint.Model ?? string.Empty,
            ApiVersion = endpoint.ApiVersion ?? string.Empty,
            Temperature = 0,
            MaxTokens = SummaryMaxTokens,
            TimeoutSeconds = endpoint.TimeoutSeconds,
            ThinkingMode = looksLikeDeepSeek ? ThinkingModes.Off : ThinkingModes.Auto,
        };
    }

    private static int FindIndex(IReadOnlyList<AiAssistantTurn> turns, Guid id)
    {
        for (var i = 0; i < turns.Count; i++)
        {
            if (turns[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }
}
