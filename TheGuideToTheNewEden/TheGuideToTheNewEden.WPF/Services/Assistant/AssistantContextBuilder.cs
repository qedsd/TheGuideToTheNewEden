using TheGuideToTheNewEden.WPF.Services.Translation.Llm;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>一次上下文组装的结果（界面提示 + 探针断言用）。</summary>
/// <param name="Messages">按时间顺序重建出来的整段历史（引擎把它插在 system 之后、本次提问之前）。</param>
/// <param name="TurnsIncluded">真正带上的轮次数。</param>
/// <param name="TurnsDropped">因超出预算被**整轮**省略的轮次数（最旧的先丢）。</param>
/// <param name="Chars">带上内容的字符数（消息正文 + 工具名与参数）。</param>
public sealed record AssistantContextResult(
    IReadOnlyList<ChatMessage> Messages,
    int TurnsIncluded,
    int TurnsDropped,
    int Chars)
{
    /// <summary>估算 token（阶段 134；旧字符预算路径也会填，便于统一显示）。</summary>
    public int Tokens { get; init; }

    /// <summary>摘要文本长度（0 = 没带摘要）。</summary>
    public int SummaryChars { get; init; }

    /// <summary>是否带上了此前生成的会话摘要。</summary>
    public bool SummaryApplied { get; init; }

    /// <summary>
    /// 因预算省略中间轮次时给模型的**系统提示后缀**（阶段 134）：
    /// 必须告诉模型"中间有内容被省略"，否则它会引用不存在的上文或误以为没说过。
    /// 由调用方拼进系统提示词（不插进消息序列，避免 Anthropic 不认中途的 system 角色）。
    /// </summary>
    public string? OmissionNote { get; init; }

    /// <summary>什么都没有带上（新对话的第一条、或上下文开关关掉时）。</summary>
    public static readonly AssistantContextResult Empty = new([], 0, 0, 0);
}

/// <summary>
/// 把**同一个对话**里此前的轮次重建成模型消息序列（阶段 132；阶段 134 加 token 预算与摘要）。
/// <para>
/// 每轮重建为：<c>user(提问)</c> → 逐轮 <c>assistant(tool_calls)</c> + <c>tool(结果)</c> → <c>assistant(最终回答)</c>。
/// 这与常见 harness 的"完整 message 数组"一致：模型能直接引用此前工具返回的原始数据，而不是只看自己写过的散文
/// （阶段 132 之前只带"问 / 答"两段文本，追问"刚才那第二条的到期时间"时模型只能再调一次工具）。
/// </para>
/// <para>
/// **记忆只存在于单个对话**：不同对话之间不共享任何内容，也没有跨对话的长期记忆。
/// </para>
/// <para>
/// **阶段 134 的预算语义**：<see cref="BuildWithBudget"/> 用 token 计费，并且调用方传进来的
/// <c>maxTokens</c> 是**已经扣掉系统提示词、工具声明、输出预留、本轮工具预留之后**的可用历史额度
/// （见 <see cref="AssistantContextBudget"/>）。摘要占用的 token 也从这里扣。
/// 超出时优先保留摘要与最近的轮次，其次用"钉住最早一轮"兜住原始目标；
/// 最新一轮单独超预算时逐级压缩它的工具结果（2000 → 800 → 300 → 120 字符），提问与回答始终保留。
/// </para>
/// <para>
/// 阶段 134 还修了两件"上下文卫生"的事：
/// ① 发生省略时返回 <see cref="AssistantContextResult.OmissionNote"/>，由调用方拼进系统提示词——
///    之前模型完全不知道中间少了内容；② 工具结果的截断不再只留一个省略号，而是带
///    "已截断，原 N 字符"的可诊断标记（<see cref="ToolArgs.TruncateForModel"/>）。
/// </para>
/// <para>纯函数：不读设置、不碰界面，因此 <c>engine</c> 探针可以离线断言"带什么、丢什么、压缩到多少"。</para>
/// </summary>
public static class AssistantContextBuilder
{
    /// <summary>
    /// 落盘时单条工具结果的上限。与引擎回填给模型的 4000 字符上限是两回事：这里更小，
    /// 避免历史文件（40 对话 × 200 轮）被工具结果撑到几十 MB。
    /// </summary>
    public const int MaxStoredResultChars = 2000;

    /// <summary>落盘时单条工具参数 JSON 的上限。</summary>
    public const int MaxStoredArgumentsChars = 600;

    /// <summary>
    /// 预算不足时**优先钉住**的最早轮次数（"头"）。
    /// <para>
    /// 长会话里最前面那一轮通常写着"我到底要干什么"；纯滑窗会把最早的目标先丢掉，模型随后开始重复取数。
    /// 头只在"它与最新一轮能一起装下"时保留，装不下就让位给最近的轮次（最新一轮永远优先）。
    /// 有摘要时不再钉头——摘要里已经写着原始目标。
    /// </para>
    /// </summary>
    public const int HeadTurns = 1;

    /// <summary>
    /// 预算不足时额外**钉住**的"关键轮"数量（阶段 150）：目前只认"含已确认写操作"的轮次。
    /// <para>
    /// 确认过的写操作是既成事实（跳桥已加、术语已改、购物车已加），一旦被滑窗丢掉，模型容易重复提议同一动作；
    /// 它的价值高于中间普通轮次，但低于"最新一轮"——所以预算不够时先放弃它、再放弃"头"。
    /// </para>
    /// </summary>
    public const int KeyTurns = 1;

    /// <summary>单轮压缩档位（从大到小试，第一个装得下的就用）。</summary>
    private static readonly int[] ResultCaps = [MaxStoredResultChars, 800, 300, 120];

    /// <summary>省略说明预留（token），保证它自己不会把预算撑破。</summary>
    private const int OmissionNoteReserveTokens = 120;

    /// <summary>
    /// 旧接口：按**字符**预算组装（阶段 132 的语义，保留给既有探针与兼容调用）。
    /// 生产路径请用 <see cref="BuildWithBudget"/>。
    /// </summary>
    public static AssistantContextResult Build(
        IReadOnlyList<AiAssistantTurn>? turns,
        AiAssistantTurn? current,
        int maxChars)
        => BuildCore(turns, current, maxChars, CostMode.Chars, summary: null, summaryUpToTurnId: null, zeroMeansUnlimited: true);

    /// <summary>
    /// 按 **token** 预算组装（阶段 134 的生产路径）。
    /// </summary>
    /// <param name="turns">该对话的全部轮次（时间顺序，含 <paramref name="current"/>）。</param>
    /// <param name="current">本次提问对应的轮次（不把自己当上文）；null = 全部轮次都算上文。</param>
    /// <param name="maxTokens">
    /// 可用的历史 token 预算（已扣掉固定前缀与本轮预留）。
    /// <b>负数 = 不限制</b>；<b>0 = 一点历史都不带</b>（预算真的用光了，不是不限——这点与旧字符接口相反，
    /// 否则"窗口太小"会被误当成"无限"）。
    /// </param>
    /// <param name="summary">此前的会话摘要（null/空 = 没有摘要）。</param>
    /// <param name="summaryUpToTurnId">摘要覆盖到的最后一条轮次 Id（该轮及更早的不会再重复带上）。</param>
    public static AssistantContextResult BuildWithBudget(
        IReadOnlyList<AiAssistantTurn>? turns,
        AiAssistantTurn? current,
        int maxTokens,
        string? summary = null,
        Guid? summaryUpToTurnId = null)
        => BuildCore(turns, current, maxTokens, CostMode.Tokens, summary, summaryUpToTurnId, zeroMeansUnlimited: false);

    /// <summary>
    /// 把摘要与省略说明拼成**系统提示后缀**（引擎追加到系统提示词末尾）。
    /// 两者都不存在时返回 null（请求体与阶段 132 完全一致）。
    /// </summary>
    public static string? BuildSystemSuffix(string? summary, string? omissionNote)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(summary))
        {
            parts.Add("【此前的对话摘要（由更早的记录压缩而来；其中的数字与 ID 仍然是当时查到的结果，可能已过期）】\n"
                      + summary.Trim());
        }

        if (!string.IsNullOrWhiteSpace(omissionNote))
        {
            parts.Add(omissionNote.Trim());
        }

        return parts.Count == 0 ? null : string.Join("\n\n", parts);
    }

    /// <summary>一轮重建后的消息与两种计费口径。</summary>
    private readonly record struct Block(List<ChatMessage> Messages, int Chars, int Tokens);

    private enum CostMode
    {
        Chars,
        Tokens,
    }

    private static AssistantContextResult BuildCore(
        IReadOnlyList<AiAssistantTurn>? turns,
        AiAssistantTurn? current,
        int maxCost,
        CostMode mode,
        string? summary,
        Guid? summaryUpToTurnId,
        bool zeroMeansUnlimited)
    {
        var summaryText = string.IsNullOrWhiteSpace(summary) ? null : summary!.Trim();

        // 摘要对应的轮次已经不在（被 200 轮上限裁掉 / 用户清空）时丢弃摘要，避免"摘要说的事+原文又带一遍"
        var coveredIndex = -1;
        if (summaryText is not null && summaryUpToTurnId is { } id)
        {
            coveredIndex = FindIndex(turns, id);
            if (coveredIndex < 0)
            {
                summaryText = null;
            }
        }
        else if (summaryText is not null)
        {
            summaryText = null;   // 没有覆盖范围就无法判断哪些轮次已被摘要 → 不用
        }

        var history = new List<AiAssistantTurn>();
        if (turns is { Count: > 0 })
        {
            for (var i = 0; i < turns.Count; i++)
            {
                var turn = turns[i];
                if (ReferenceEquals(turn, current))
                {
                    break;   // 只带"当前这条之前"的轮次（并发发送时，后发的轮次不是任何人的上文）
                }

                if (i <= coveredIndex)
                {
                    continue;   // 已被摘要覆盖
                }

                if (CanBeContext(turn))
                {
                    history.Add(turn);
                }
            }
        }

        var summaryCost = 0;
        if (summaryText is not null)
        {
            summaryCost = mode == CostMode.Chars
                ? summaryText.Length + 64
                : AssistantTokenEstimator.Estimate(summaryText) + 8;
        }

        if (history.Count == 0)
        {
            return new AssistantContextResult([], 0, 0, 0)
            {
                Tokens = summaryCost,
                SummaryChars = summaryText?.Length ?? 0,
                SummaryApplied = summaryText is not null,
            };
        }

        // token 接口里 0 表示"预算用光、一点历史都不带"（不是不限）；摘要仍然可以带。
        if (maxCost == 0 && !zeroMeansUnlimited)
        {
            return new AssistantContextResult([], 0, history.Count, 0)
            {
                Tokens = summaryCost,
                SummaryChars = summaryText?.Length ?? 0,
                SummaryApplied = summaryText is not null,
                OmissionNote = history.Count == 0
                    ? null
                    : BuildOmissionNote(history.Count, summaryText is not null),
            };
        }

        // 不限制：整段历史按落盘上限原样带上
        if (maxCost < 0 || (maxCost == 0 && zeroMeansUnlimited))
        {
            var all = history.Select(turn => BuildTurn(turn, MaxStoredResultChars)).ToList();
            var messages = all.SelectMany(block => block.Messages).ToList();
            return new AssistantContextResult(
                messages,
                all.Count,
                0,
                all.Sum(block => block.Chars) + (summaryText?.Length ?? 0))
            {
                Tokens = all.Sum(block => block.Tokens) + summaryCost,
                SummaryChars = summaryText?.Length ?? 0,
                SummaryApplied = summaryText is not null,
            };
        }

        var blocks = history.Select(turn => BuildTurn(turn, MaxStoredResultChars)).ToList();
        var newest = blocks.Count - 1;
        // 字符模式是阶段 132 的旧语义，不能扣预留（否则既有断言与历史行为都会变）；
        // token 模式才给省略说明留一点空间。
        var noteReserve = mode == CostMode.Tokens ? OmissionNoteReserveTokens : 0;
        var budget = Math.Max(0, maxCost - summaryCost - noteReserve);

        // ① "头"：最早的那几轮（只钉"早于最新"的；有摘要时不钉头，摘要里已有原始目标）
        var head = new List<int>();
        if (summaryText is null)
        {
            for (var i = 0; i < Math.Min(HeadTurns, blocks.Count) && i < newest; i++)
            {
                head.Add(i);
            }
        }

        // ①b 关键轮（阶段 150）：最近一次"含已确认写操作"的轮次（同样只在没摘要时钉，摘要里已有事实）
        var key = new List<int>();
        if (summaryText is null)
        {
            for (var i = newest - 1; i >= 0 && key.Count < KeyTurns; i--)
            {
                if (head.Contains(i))
                {
                    continue;
                }

                if (history[i].Steps?.Any(step => step.IsWrite) == true)
                {
                    key.Add(i);
                }
            }
        }

        // ② 头 + 关键轮必须能与最新一轮共存：装不下先丢关键轮，再装不下才放弃头（最近的数据优先）
        var pinned = new List<int>(head);
        pinned.AddRange(key);
        var total = pinned.Sum(index => CostOf(blocks[index], mode));
        if (total + CostOf(blocks[newest], mode) > budget)
        {
            pinned = new List<int>(head);
            total = pinned.Sum(index => CostOf(blocks[index], mode));
            if (total + CostOf(blocks[newest], mode) > budget)
            {
                pinned.Clear();
                total = 0;
            }
        }

        var selected = new List<int>(pinned);

        // ③ 最近的优先：从最新往前填，装不下就停（中间轮次整轮省略）
        for (var i = newest; i >= 0; i--)
        {
            if (selected.Contains(i))
            {
                continue;
            }

            var block = blocks[i];
            if (total + CostOf(block, mode) > budget)
            {
                if (i != newest)
                {
                    break;
                }

                block = BuildCompressed(history[i], budget - total, mode);   // 最新一轮：压缩工具结果后仍然带上
                blocks[i] = block;
            }

            selected.Add(i);
            total += CostOf(block, mode);
        }

        selected.Sort();
        var finalMessages = new List<ChatMessage>(selected.Sum(index => blocks[index].Messages.Count));
        foreach (var index in selected)
        {
            finalMessages.AddRange(blocks[index].Messages);
        }

        var dropped = history.Count - selected.Count;
        var omission = dropped > 0 ? BuildOmissionNote(dropped, summaryText is not null) : null;

        return new AssistantContextResult(
            finalMessages,
            selected.Count,
            dropped,
            finalMessages.Sum(message => message.Content?.Length ?? 0)
                + finalMessages.Sum(message => message.ToolCalls?.Sum(call => call.Name.Length + call.ArgumentsJson.Length) ?? 0)
                + (summaryText?.Length ?? 0))
        {
            Tokens = blocks.Where((_, index) => selected.Contains(index)).Sum(block => block.Tokens) + summaryCost
                + (omission is null ? 0 : AssistantTokenEstimator.Estimate(omission)),
            SummaryChars = summaryText?.Length ?? 0,
            SummaryApplied = summaryText is not null,
            OmissionNote = omission,
        };
    }

    private static int CostOf(Block block, CostMode mode) => mode == CostMode.Chars ? block.Chars : block.Tokens;

    /// <summary>这一轮能不能当后文的上文（成功、有回答、不是失败/被停止）。</summary>
    public static bool CanBeContext(AiAssistantTurn turn)
        => turn.Success
           && !turn.Stopped
           && string.IsNullOrWhiteSpace(turn.Error)
           && !string.IsNullOrWhiteSpace(turn.AnswerText);

    /// <summary>估算一轮重建后占用的 token（压缩服务判断"要不要摘要"用）。</summary>
    public static int EstimateTurnTokens(AiAssistantTurn turn, int resultCap = MaxStoredResultChars)
        => BuildTurn(turn, resultCap).Tokens;

    /// <summary>重建一轮：提问 → 工具往返（按轮分组）→ 最终回答。</summary>
    private static Block BuildTurn(AiAssistantTurn turn, int resultCap)
    {
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(turn.UserText))
        {
            messages.Add(ChatMessage.User(turn.UserText));
        }

        foreach (var group in GroupByRound(turn))
        {
            messages.Add(ChatMessage.AssistantToolCalls(
                group.Select(step => new ChatToolCall(
                    step.CallId,
                    step.ToolName,
                    string.IsNullOrWhiteSpace(step.ArgumentsJson) ? "{}" : step.ArgumentsJson)).ToList()));

            foreach (var step in group)
            {
                messages.Add(ChatMessage.Tool(step.CallId, StepText(step, resultCap), step.ToolName));
            }
        }

        messages.Add(ChatMessage.Assistant(turn.AnswerText));
        return new Block(messages, CountChars(messages), AssistantTokenEstimator.EstimateMessages(messages));
    }

    /// <summary>最新一轮单独超预算时的压缩：逐级缩小工具结果，提问与回答不动。</summary>
    private static Block BuildCompressed(AiAssistantTurn turn, int maxCost, CostMode mode)
    {
        var fallback = BuildTurn(turn, ResultCaps[0]);
        if (maxCost <= 0)
        {
            return fallback;
        }

        foreach (var cap in ResultCaps)
        {
            var candidate = BuildTurn(turn, cap);
            if (CostOf(candidate, mode) <= maxCost)
            {
                return candidate;
            }

            fallback = candidate;
        }

        return fallback;   // 最小档仍装不下：照样带上（提问与回答不能丢）
    }

    /// <summary>
    /// 工具步骤按"同一条 assistant 消息"分组：引擎一轮里的多个调用是
    /// **一条** assistant(tool_calls) + **多条** tool 消息，重建时必须还原成同一形状。
    /// </summary>
    private static IEnumerable<List<AiAssistantStep>> GroupByRound(AiAssistantTurn turn)
        => (turn.Steps ?? [])
            .Where(step => !string.IsNullOrWhiteSpace(step.CallId) && !string.IsNullOrWhiteSpace(step.ToolName))
            .GroupBy(step => step.Round)
            .OrderBy(group => group.Key)
            .Select(group => group.ToList());

    private static string StepText(AiAssistantStep step, int cap)
    {
        var text = !string.IsNullOrWhiteSpace(step.ResultText) ? step.ResultText : step.ResultSummary;
        return ToolArgs.TruncateForModel(text ?? string.Empty, cap);
    }

    private static int CountChars(IReadOnlyList<ChatMessage> messages)
    {
        var total = 0;
        foreach (var message in messages)
        {
            total += message.Content?.Length ?? 0;
            if (message.ToolCalls is { Count: > 0 })
            {
                total += message.ToolCalls.Sum(call => call.Name.Length + call.ArgumentsJson.Length);
            }
        }

        return total;
    }

    private static string BuildOmissionNote(int dropped, bool hasSummary)
        => hasSummary
            ? $"（上下文说明：因预算限制省略了 {dropped} 轮较早的往来，只保留了摘要与最近的内容；如果需要其中细节，请重新查询对应数据。）"
            : $"（上下文说明：因预算限制省略了 {dropped} 轮较早的往来，只保留了最近的内容；如果需要其中细节，请重新查询对应数据。）";

    private static int FindIndex(IReadOnlyList<AiAssistantTurn>? turns, Guid id)
    {
        if (turns is null)
        {
            return -1;
        }

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
