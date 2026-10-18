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
    /// <summary>什么都没有带上（新对话的第一条、或上下文开关关掉时）。</summary>
    public static readonly AssistantContextResult Empty = new([], 0, 0, 0);
}

/// <summary>
/// 把**同一个对话**里此前的轮次重建成模型消息序列（阶段 132）。
/// <para>
/// 每轮重建为：<c>user(提问)</c> → 逐轮 <c>assistant(tool_calls)</c> + <c>tool(结果)</c> → <c>assistant(最终回答)</c>。
/// 这与常见 harness 的"完整 message 数组"一致：模型能直接引用此前工具返回的原始数据，而不是只看自己写过的散文
/// （阶段 132 之前只带"问 / 答"两段文本，追问"刚才那第二条的到期时间"时模型只能再调一次工具）。
/// </para>
/// <para>
/// **记忆只存在于单个对话**：不同对话之间不共享任何内容，也没有跨对话的长期记忆。
/// 超出 <paramref name="maxChars"/> 时**从中间最旧的轮次开始整轮省略**（不会丢半轮）：最近的轮次优先保留，
/// 最早的一轮（"头"，见 <see cref="HeadTurns"/>）在能与最新一轮共存时也钉住——长会话里它写着"我到底要干什么"；
/// 若最新一轮单独就超预算，则逐级压缩这一轮的工具结果（2000 → 800 → 300 → 120 字符），提问与回答始终保留。
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
    /// </para>
    /// </summary>
    public const int HeadTurns = 1;

    /// <summary>单轮压缩档位（从大到小试，第一个装得下的就用）。</summary>
    private static readonly int[] ResultCaps = [MaxStoredResultChars, 800, 300, 120];

    /// <summary>
    /// 组装上下文。
    /// </summary>
    /// <param name="turns">该对话的全部轮次（时间顺序，含 <paramref name="current"/>）。</param>
    /// <param name="current">本次提问对应的轮次（不把自己当上文）；null = 全部轮次都算上文。</param>
    /// <param name="maxChars">字符预算；<c>0</c> 或负数 = 不限制。</param>
    public static AssistantContextResult Build(IReadOnlyList<AiAssistantTurn>? turns, AiAssistantTurn? current, int maxChars)
    {
        if (turns is not { Count: > 0 })
        {
            return AssistantContextResult.Empty;
        }

        var history = new List<AiAssistantTurn>();
        foreach (var turn in turns)
        {
            if (ReferenceEquals(turn, current))
            {
                break;   // 只带"当前这条之前"的轮次（并发发送时，后发的轮次不是任何人的上文）
            }

            if (CanBeContext(turn))
            {
                history.Add(turn);
            }
        }

        if (history.Count == 0)
        {
            return AssistantContextResult.Empty;
        }

        // 不限制：整段历史按落盘上限原样带上
        if (maxChars <= 0)
        {
            var all = history.Select(turn => BuildTurn(turn, MaxStoredResultChars)).ToList();
            return new AssistantContextResult(
                all.SelectMany(block => block.Messages).ToList(),
                all.Count,
                0,
                all.Sum(block => block.Chars));
        }

        var blocks = history.Select(turn => BuildTurn(turn, MaxStoredResultChars)).ToList();
        var newest = blocks.Count - 1;

        // ① "头"：最早的那几轮（只钉"早于最新"的；历史只有一轮时它本身就是最新轮）
        var pinned = new List<int>();
        for (var i = 0; i < Math.Min(HeadTurns, blocks.Count) && i < newest; i++)
        {
            pinned.Add(i);
        }

        // ② 头必须能与最新一轮共存，否则放弃头（预算全给最近的轮次）
        var total = pinned.Sum(index => blocks[index].Chars);
        if (total + blocks[newest].Chars > maxChars)
        {
            pinned.Clear();
            total = 0;
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
            if (total + block.Chars > maxChars)
            {
                if (i != newest)
                {
                    break;
                }

                block = BuildCompressed(history[i], maxChars - total);   // 最新一轮：压缩工具结果后仍然带上
                blocks[i] = block;
            }

            selected.Add(i);
            total += block.Chars;
        }

        selected.Sort();
        var messages = new List<ChatMessage>(selected.Sum(index => blocks[index].Messages.Count));
        foreach (var index in selected)
        {
            messages.AddRange(blocks[index].Messages);
        }

        return new AssistantContextResult(messages, selected.Count, history.Count - selected.Count, total);
    }

    /// <summary>这一轮能不能当后文的上文（成功、有回答、不是失败/被停止）。</summary>
    public static bool CanBeContext(AiAssistantTurn turn)
        => turn.Success
           && !turn.Stopped
           && string.IsNullOrWhiteSpace(turn.Error)
           && !string.IsNullOrWhiteSpace(turn.AnswerText);

    /// <summary>重建一轮：提问 → 工具往返（按轮分组）→ 最终回答。</summary>
    private static (List<ChatMessage> Messages, int Chars) BuildTurn(AiAssistantTurn turn, int resultCap)
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
        return (messages, CountChars(messages));
    }

    /// <summary>最新一轮单独超预算时的压缩：逐级缩小工具结果，提问与回答不动。</summary>
    private static (List<ChatMessage> Messages, int Chars) BuildCompressed(AiAssistantTurn turn, int maxChars)
    {
        var fallback = BuildTurn(turn, ResultCaps[0]);
        if (maxChars <= 0)
        {
            return fallback;
        }

        foreach (var cap in ResultCaps)
        {
            var candidate = BuildTurn(turn, cap);
            if (candidate.Chars <= maxChars)
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
        return Truncate(text ?? string.Empty, cap);
    }

    private static string Truncate(string text, int max)
        => max > 0 && text.Length > max ? text[..max] : text;

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
}
