using System.Text;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.Services.Translation.Llm;

namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// 引擎循环的各条分支（**本地 mock 大模型**，端点经 <see cref="AssistantContext.Endpoint"/> 注入，
/// 不动用户的 settings.json）：无工具轮省钱路径 / 工具轮往返 / 循环上限 / 未知工具 / 工具抛错 /
/// 工具超时 / 结果截断 / 写操作确认（应用与拒绝）/ 上下文 / 自定义提示词 / 取消传播。
/// <para>
/// mock 脚本按"请求序号"给答案：**首个非流式请求**要求调工具，其后的非流式请求给普通回答
/// （真实模型看到工具结果后就是这么答的）；带 <c>stream:true</c> 的请求返回 SSE。
/// </para>
/// </summary>
internal static class EngineSuite
{
    public static async Task RunAsync(Check c)
    {
        c.Section("EngineSuite · 无工具轮（简单问答只发一次请求）");
        await NoToolAsync(c);

        c.Section("EngineSuite · 单工具轮 + 流式最终回答");
        await SingleToolAsync(c);

        c.Section("EngineSuite · 循环上限");
        await LoopCapAsync(c);

        c.Section("EngineSuite · 异常分支（未知工具 / 抛错 / 超时 / 结果截断）");
        await UnknownToolAsync(c);
        await ToolThrowsAsync(c);
        await ToolTimeoutAsync(c);
        await LongResultAsync(c);

        c.Section("EngineSuite · 写操作确认（挂起 / 应用 / 拒绝）");
        await ConfirmApplyAsync(c);
        await ConfirmRejectAsync(c);

        c.Section("EngineSuite · 上下文与提示词");
        await ContextAsync(c);
        await CustomPromptAsync(c);

        c.Section("EngineSuite · 取消传播");
        await CancelAsync(c);
    }

    // ============================ 各场景 ============================

    private static async Task NoToolAsync(Check c)
    {
        using var server = new MockLlmServer((_, _) => MockLlmServer.Text("直接回答"));
        var turn = await new AiAssistantEngine().SendAsync("你好", Ctx(server), CancellationToken.None);
        var final = await ReadFinalAsync(turn);

        c.Equal(server.RequestBodies.Count, 1, "无工具轮只发 1 次请求（省掉重复生成）");
        c.Equal(turn.Steps.Count, 0, "没有工具执行");
        c.Equal(final, "直接回答", "最终回答来自首轮文本");
        c.That(ProbeJson.HasProperty(server.RequestBodies[0], "tools"), "请求体带 tools（模型能看到工具清单）");
        c.That(ProbeJson.Contains(server.RequestBodies[0], "严禁编造"), "内置系统提示词已下发");
        c.That(ProbeJson.Contains(server.RequestBodies[0], "倒货/比价/走势"), "内置提示词规则 8（AI 不得自行算数字）已下发");
        c.That(!IsStreaming(server.RequestBodies[0]), "工具轮是非流式");
    }

    private static async Task SingleToolAsync(Check c)
    {
        using var server = new MockLlmServer((index, body) => IsStreaming(body)
            ? MockLlmServer.Stream("最终", "回答")
            : index == 0
                ? MockLlmServer.ToolCall("call_1", "probe_echo", """{"value":"hello"}""")
                : MockLlmServer.Text("看到工具结果了"));

        ProbeTools.ResetCounters();
        var turn = await new AiAssistantEngine().SendAsync("echo", Ctx(server), CancellationToken.None);
        var final = await ReadFinalAsync(turn);

        c.Equal(turn.ToolRounds, 1, "工具轮数 = 1");
        c.Equal(turn.Steps.Count, 1, "记录了 1 个工具步骤");
        c.That(turn.Steps[0].Success, "步骤标记成功");
        c.Equal(turn.Steps[0].Name, "probe_echo", "步骤里的工具名");
        c.Equal(turn.Steps[0].ParamSummary, "value=hello", "步骤用的是工具给的参数摘要");
        c.Equal(ProbeTools.EchoCalls, 1, "工具真的被执行了一次");
        c.Equal(server.RequestBodies.Count, 3, "请求序列：带 tools（非流式）→ 工具结果（非流式）→ 最终回答（流式）");

        var second = server.RequestBodies[1];
        var messageCount = ProbeJson.ArrayLength(second, "messages");
        c.Equal(ProbeJson.RoleAt(second, "messages", messageCount - 1), "tool", "第二次请求的最后一条消息是 tool 结果");
        c.That(ProbeJson.Contains(second, "echo: hello"), "tool 消息带工具结果文本");
        c.That(ProbeJson.HasProperty(second, "tool_calls"), "第二次请求回传了 assistant 的 tool_calls（协议要求）");
        c.That(ProbeJson.Contains(second, "call_1"), "tool 消息带对应的 tool_call_id");

        var third = server.RequestBodies[2];
        c.That(IsStreaming(third), "最终回答轮是流式");
        c.That(!ProbeJson.HasProperty(third, "tools"), "最终回答轮不再带 tools（避免模型又调工具）");
        c.Equal(final, "最终回答", "流式增量按顺序拼接");
        c.That(turn.TotalUsage.InputTokens > 0, "累计了 token 用量");
    }

    private static async Task LoopCapAsync(Check c)
    {
        // 故意"永远要求调工具"：验证上限兜底
        using var server = new MockLlmServer((_, body) => IsStreaming(body)
            ? MockLlmServer.Stream("基于已有信息作答")
            : MockLlmServer.ToolCall("c", "probe_echo", """{"value":"x"}"""));

        ProbeTools.ResetCounters();
        var turn = await new AiAssistantEngine(maxToolRounds: 2).SendAsync("一直调工具", Ctx(server), CancellationToken.None);
        var final = await ReadFinalAsync(turn);

        c.Equal(turn.ToolRounds, 3, "maxToolRounds=2 时第 3 轮触发上限并停止");
        c.Equal(server.RequestBodies.Count, 4, "3 次工具轮 + 1 次流式最终回答");
        c.That(ProbeJson.Contains(server.RequestBodies[3], "已达到最大工具轮数"), "上限提示已回填给模型");
        c.That(final.Length > 0, "到上限后仍给出最终回答（不卡死）");
    }

    private static async Task UnknownToolAsync(Check c)
    {
        using var server = new MockLlmServer((index, body) => IsStreaming(body)
            ? MockLlmServer.Stream("好的")
            : index == 0
                ? MockLlmServer.ToolCall("c1", "probe_nope", "{}")
                : MockLlmServer.Text("换个工具试试"));

        var turn = await new AiAssistantEngine().SendAsync("幻觉工具", Ctx(server), CancellationToken.None);
        var final = await ReadFinalAsync(turn);

        c.Equal(turn.Steps.Count, 1, "未知工具也记一条步骤（它同样是一次 tool 往返，重建历史时不能悬空）");
        c.Equal(turn.Steps[0].Name, "probe_nope", "步骤记的是模型报出的工具名");
        c.That(!turn.Steps[0].Success, "步骤标记为失败");
        c.That(ProbeJson.Contains(server.RequestBodies[1], "未知工具"), "回填了「未知工具」提示给模型");
        c.That(final.Length > 0, "对话未中断");
    }

    private static async Task ToolThrowsAsync(Check c)
    {
        using var server = new MockLlmServer((index, body) => IsStreaming(body)
            ? MockLlmServer.Stream("失败后仍继续")
            : index == 0
                ? MockLlmServer.ToolCall("c1", "probe_throw", "{}")
                : MockLlmServer.Text("知道了"));

        ProbeTools.ResetCounters();
        var turn = await new AiAssistantEngine().SendAsync("抛错", Ctx(server), CancellationToken.None);
        var final = await ReadFinalAsync(turn);

        c.Equal(ProbeTools.ThrowCalls, 1, "工具被执行到并抛了异常");
        c.Equal(turn.Steps.Count, 1, "异常也记一条步骤");
        c.That(!turn.Steps[0].Success, "步骤标记失败");
        c.That(ProbeJson.Contains(server.RequestBodies[1], "工具执行失败"), "失败原因回填给模型");
        c.That(final.Length > 0, "工具抛异常**不中断**对话");
    }

    private static async Task ToolTimeoutAsync(Check c)
    {
        using var server = new MockLlmServer((index, body) => IsStreaming(body)
            ? MockLlmServer.Stream("超时后继续")
            : index == 0
                ? MockLlmServer.ToolCall("c1", "probe_timeout", "{}")
                : MockLlmServer.Text("知道了"));

        ProbeTools.ResetCounters();
        var turn = await new AiAssistantEngine().SendAsync("超时", Ctx(server), CancellationToken.None);
        var final = await ReadFinalAsync(turn);

        c.Equal(ProbeTools.TimeoutCalls, 1, "工具被执行");
        c.That(turn.Steps.Count == 1 && !turn.Steps[0].Success, "工具自身超时 → 步骤失败");
        c.That(ProbeJson.Contains(server.RequestBodies[1], "超时或中断"), "超时按「工具错误」回填，而不是当成用户取消");
        c.That(final.Length > 0, "超时后对话继续");
    }

    private static async Task LongResultAsync(Check c)
    {
        using var server = new MockLlmServer((index, body) => IsStreaming(body)
            ? MockLlmServer.Stream("已截断")
            : index == 0
                ? MockLlmServer.ToolCall("c1", "probe_long", "{}")
                : MockLlmServer.Text("知道了"));

        ProbeTools.ResetCounters();
        var turn = await new AiAssistantEngine().SendAsync("超长", Ctx(server), CancellationToken.None);
        _ = await ReadFinalAsync(turn);

        c.Equal(ProbeTools.LongCalls, 1, "超长工具被执行");
        c.That(!ProbeJson.Contains(server.RequestBodies[1], ProbeTools.LongTailMarker),
            $"回填给模型的工具结果被截断（尾部标记 {ProbeTools.LongTailMarker} 不应出现在请求体里）");
        c.That(ProbeJson.Contains(server.RequestBodies[1], "…"), "截断处有省略号标记");
    }

    private static async Task ConfirmApplyAsync(Check c)
    {
        using var server = new MockLlmServer((index, body) => IsStreaming(body)
            ? MockLlmServer.Stream("已按你的确认执行")
            : index == 0
                ? MockLlmServer.ToolCall("c1", "probe_write", """{"value":"x"}""")
                : MockLlmServer.Text("继续"));

        ProbeTools.ResetCounters();
        var turn = await new AiAssistantEngine().SendAsync("写一下", Ctx(server), CancellationToken.None);

        c.Equal(turn.PendingConfirmations?.Count ?? 0, 1, "写操作挂起 1 条待确认");
        c.Equal(turn.PendingConfirmations![0].ToolName, "probe_write", "挂起动作带工具名");
        c.That(turn.PendingConfirmations[0].Id is { Length: > 0 }, "挂起动作带调用 Id（确认后要回填 tool 消息）");
        c.Equal(ProbeTools.WriteCalls, 0, "**挂起时不执行**（确认前零副作用）");
        c.Equal(turn.Steps.Count, 0, "挂起时没有步骤");
        c.Equal(server.RequestBodies.Count, 1, "挂起后不再向模型发请求");

        var beforeResume = await ReadFinalAsync(turn);
        c.Equal(beforeResume, string.Empty, "挂起中读最终回答为空（界面此时显示确认卡片）");

        await turn.ResumeAsync(true, CancellationToken.None);
        c.Equal(ProbeTools.WriteCalls, 1, "确认后执行了写操作");
        c.Equal(turn.Steps.Count, 1, "确认后产生步骤");
        var afterResume = await ReadFinalAsync(turn);
        c.Equal(afterResume, "已按你的确认执行", "确认后能读到最终回答");
    }

    private static async Task ConfirmRejectAsync(Check c)
    {
        using var server = new MockLlmServer((index, body) => IsStreaming(body)
            ? MockLlmServer.Stream("好，那就不做")
            : index == 0
                ? MockLlmServer.ToolCall("c1", "probe_write", """{"value":"x"}""")
                : MockLlmServer.Text("继续"));

        ProbeTools.ResetCounters();
        var turn = await new AiAssistantEngine().SendAsync("写一下", Ctx(server), CancellationToken.None);
        c.Equal(turn.PendingConfirmations?.Count ?? 0, 1, "写操作挂起");

        await turn.ResumeAsync(false, CancellationToken.None);
        c.Equal(ProbeTools.WriteCalls, 0, "**拒绝后不执行**（零写入）");
        c.Equal(turn.Steps.Count, 1, "拒绝也记一条步骤（重建历史时不能留下悬空的 tool_calls）");
        c.That(!turn.Steps[0].Success && turn.Steps[0].CallId is { Length: > 0 }, "拒绝步骤带调用 Id 且标记失败");
        c.That(ProbeJson.Contains(server.RequestBodies[^1], "未确认"), "拒绝的原因回填给模型");
        var final = await ReadFinalAsync(turn);
        c.Equal(final, "好，那就不做", "拒绝后对话继续并给出回答");
    }

    private static async Task ContextAsync(Check c)
    {
        // ① 引擎：上下文消息按顺序插在 system 之后、本次提问之前
        using var server = new MockLlmServer((_, _) => MockLlmServer.Text("好的"));
        var turn = await new AiAssistantEngine().SendAsync(
            "第二个问题",
            Ctx(server, context: [ChatMessage.User("第一个问题"), ChatMessage.Assistant("第一个回答")]),
            CancellationToken.None);
        _ = await ReadFinalAsync(turn);

        var body = server.RequestBodies[0];
        c.That(ProbeJson.Contains(body, "第一个问题") && ProbeJson.Contains(body, "第一个回答"), "上下文消息已下发");
        var flat = ProbeJson.Flatten(body);
        var question = flat.IndexOf("第一个问题", StringComparison.Ordinal);
        var answer = flat.IndexOf("第一个回答", StringComparison.Ordinal);
        var current = flat.IndexOf("第二个问题", StringComparison.Ordinal);
        c.That(question >= 0 && answer > question && current > answer, "顺序：上文问题 → 上文回答 → 本次提问");

        using var server2 = new MockLlmServer((_, _) => MockLlmServer.Text("好的"));
        var turn2 = await new AiAssistantEngine().SendAsync("只有这一问", Ctx(server2), CancellationToken.None);
        _ = await ReadFinalAsync(turn2);
        c.That(!ProbeJson.Contains(server2.RequestBodies[0], "第一个问题"), "不传上下文时不含历史问答");

        ContextBuilder(c);
    }

    /// <summary>
    /// 阶段 132：把"轮次历史"重建为消息序列（<see cref="AssistantContextBuilder"/> 是纯函数，不需要引擎与网络）。
    /// 覆盖：工具往返被完整重放 / 同轮多调用合并 / 跨轮顺序 / 非法序列守卫 / 预算裁剪 / 单轮压缩 / 旧数据兜底。
    /// </summary>
    private static void ContextBuilder(Check c)
    {
        // ① 工具往返（assistant(tool_calls) → tool(结果) → assistant(回答)）
        var withTools = Turn(
            "巨神兵级多少钱",
            "最低卖价约 5.2 亿",
            Step("call_1", 1, "market_orders", """{"typeName":"巨神兵级"}""", "最低卖价 520,000,000 ISK（TypeId 24692）"));
        var replay = AssistantContextBuilder.Build([withTools], null, 0);
        c.Equal(replay.TurnsIncluded, 1, "带上一轮");
        c.Equal(replay.Messages.Count, 4, "一条提问 + assistant(tool_calls) + tool 结果 + 最终回答（4 条消息）");
        c.Equal(replay.Messages[0].Content, "巨神兵级多少钱", "第 1 条是提问");
        c.Equal(replay.Messages[1].ToolCalls?.Count ?? 0, 1, "第 2 条是带 1 个 tool_call 的 assistant 消息");
        c.Equal(replay.Messages[1].ToolCalls![0].Id, "call_1", "tool_call 带调用 Id");
        c.Equal(replay.Messages[1].ToolCalls[0].Name, "market_orders", "tool_call 带工具名");
        c.That(replay.Messages[1].ToolCalls[0].ArgumentsJson.Contains("巨神兵级", StringComparison.Ordinal),
            "tool_call 带回参数 JSON（重建后模型看到的参数与当初一致）");
        c.Equal(replay.Messages[2].Role, ChatRole.Tool, "第 3 条是 tool 结果");
        c.Equal(replay.Messages[2].ToolCallId, "call_1", "tool 消息用 CallId 与调用配对");
        c.That((replay.Messages[2].Content ?? string.Empty).Contains("520,000,000", StringComparison.Ordinal),
            "**工具结果全文**进入上下文，而不是只有 160 字符的界面摘要");
        c.Equal(replay.Messages[3].Content, "最低卖价约 5.2 亿", "第 4 条是最终回答");
        c.That(ToolCallsPaired(replay.Messages), "assistant(tool_calls) 后必有对应 tool 结果（消息序列合法）");

        // ② 同一轮里的多个调用合并成**一条** assistant 消息（不能拆成两条）
        var multi = Turn(
            "两件事",
            "都办好了",
            Step("a1", 1, "market_orders", """{"typeName":"A"}""", "A 的结果"),
            Step("a2", 1, "market_history", """{"typeName":"B"}""", "B 的结果"));
        var multiReplay = AssistantContextBuilder.Build([multi], null, 0);
        c.Equal(multiReplay.Messages.Count, 5, "提问 + assistant(2 个 tool_calls) + 2 条 tool + 回答");
        c.Equal(multiReplay.Messages[1].ToolCalls?.Count ?? 0, 2, "同轮两个调用在同一条 assistant 消息里");
        c.That(ToolCallsPaired(multiReplay.Messages), "同轮多调用的配对完整");

        // ③ 跨轮：第二轮的工具往返与第一轮各自成组，顺序不变
        var twoRounds = Turn(
            "再看看",
            "看完了",
            Step("b1", 1, "market_orders", "{}", "第一轮结果"),
            Step("b2", 2, "market_history", "{}", "第二轮结果"));
        var twoRoundReplay = AssistantContextBuilder.Build([twoRounds], null, 0);
        c.Equal(twoRoundReplay.Messages.Count, 6, "提问 + 2 组（assistant + tool）+ 回答");
        var bodyOrder = string.Join(
            "|",
            twoRoundReplay.Messages.Select(m => m.Role == ChatRole.Tool ? m.Content : m.Role.ToString()));
        c.That(bodyOrder.IndexOf("第一轮结果", StringComparison.Ordinal) < bodyOrder.IndexOf("第二轮结果", StringComparison.Ordinal),
            "两轮工具结果保持时间顺序");

        // ④ 预算：**最近的优先**，最早的"头"能共存就钉住，中间的被整轮省略
        var trimmed = AssistantContextBuilder.Build(
            [Turn("第一条", new string('a', 400)), Turn("第二条", new string('b', 400)), Turn("第三条", new string('c', 400))],
            null,
            900);
        c.Equal(trimmed.TurnsIncluded, 2, "预算 900 只装得下两轮（每轮约 400 字符）");
        c.Equal(trimmed.TurnsDropped, 1, "被省略的是**中间**那一轮（整轮省略，不丢半轮）");
        c.That(trimmed.Messages.Any(m => m.Content == "第一条"), "最早的「头」被钉住（长会话里的原始目标）");
        c.That(trimmed.Messages.Any(m => m.Content == "第三条"), "最新一轮一定在");
        c.That(trimmed.Messages.All(m => m.Content != "第二条"), "中间那一轮被省略");
        var trimmedTexts = trimmed.Messages.Select(message => message.Content).ToList();
        c.That(trimmedTexts.IndexOf("第一条") < trimmedTexts.IndexOf("第三条"),
            "顺序仍是时间顺序（头在前、最新在后）");

        // ④b 头连"与最新一轮共存"都做不到 → 让位给最近的轮次
        var droppedHead = AssistantContextBuilder.Build(
            [Turn("大目标", new string('a', 400)), Turn("最新", new string('b', 400))],
            null,
            500);
        c.Equal(droppedHead.TurnsIncluded, 1, "预算太小：只带最新一轮");
        c.That(droppedHead.Messages.Any(m => m.Content == "最新"), "最新一轮仍在");
        c.That(droppedHead.Messages.All(m => m.Content != "大目标"), "预算不够时放弃「头」（最近的数据优先）");

        // ④c 预算 0 = 不限制：整段历史都带上
        var unlimited = AssistantContextBuilder.Build([Turn("一", "A"), Turn("二", "B"), Turn("三", "C")], null, 0);
        c.Equal(unlimited.TurnsIncluded, 3, "预算 0 = 不限制：整段历史都带上");
        c.Equal(unlimited.TurnsDropped, 0, "不限制时没有省略");

        // ⑤ 最新一轮自己就超预算 → 压缩它的工具结果，提问与回答保留
        var heavy = Turn("压我", "结论", Step("c9", 1, "market_orders", """{"x":1}""", new string('z', 2000)));
        var squeezed = AssistantContextBuilder.Build([heavy], null, 500);
        c.Equal(squeezed.TurnsDropped, 0, "最新一轮不会被丢");
        c.Equal(squeezed.Messages[0].Content, "压我", "压缩时提问保留");
        c.Equal(squeezed.Messages[^1].Content, "结论", "压缩时回答保留");
        var toolLength = squeezed.Messages.First(m => m.Role == ChatRole.Tool).Content?.Length ?? 0;
        c.That(toolLength is > 0 and <= 300, $"工具结果被逐级压缩到 {toolLength} 字符（≤300）");

        // ⑥ 当前这条不算自己的上文
        var first = Turn("甲", "A");
        var second = Turn("乙", "B");
        var beforeCurrent = AssistantContextBuilder.Build([first, second], second, 0);
        c.Equal(beforeCurrent.TurnsIncluded, 1, "当前这条不算上文");
        c.That(beforeCurrent.Messages.All(m => m.Content != "乙"), "当前提问不在上下文里");

        // ⑦ 失败 / 被停止的轮次不进上下文
        var failed = Turn("失败的", "半截回答");
        failed.Success = false;
        var stopped = Turn("停掉的", "半截回答");
        stopped.Stopped = true;
        c.Equal(AssistantContextBuilder.Build([failed, stopped], null, 0).Messages.Count, 0,
            "失败与被停止的轮次都不进上下文");

        // ⑧ 旧数据：步骤没有 CallId → 只重放问答，不造非法的工具往返
        var legacy = new AiAssistantTurn
        {
            UserText = "老对话",
            AnswerText = "老回答",
            Success = true,
            Steps = [new AiAssistantStep { ToolName = "market_orders", ResultSummary = "旧摘要" }],
        };
        var legacyReplay = AssistantContextBuilder.Build([legacy], null, 0);
        c.Equal(legacyReplay.Messages.Count, 2, "旧数据（步骤缺 CallId）只重放问答");
        c.That(legacyReplay.Messages.All(m => m.Role is ChatRole.User or ChatRole.Assistant),
            "没有 CallId 的步骤不会生成悬空的 tool 消息");
    }

    /// <summary>消息序列合法性：每条 assistant(tool_calls) 后面必须紧跟它全部调用的 tool 结果。</summary>
    private static bool ToolCallsPaired(IReadOnlyList<ChatMessage> messages)
    {
        for (var i = 0; i < messages.Count; i++)
        {
            if (messages[i].ToolCalls is not { Count: > 0 } calls)
            {
                continue;
            }

            var ids = messages
                .Skip(i + 1)
                .TakeWhile(message => message.Role == ChatRole.Tool)
                .Select(message => message.ToolCallId)
                .ToHashSet(StringComparer.Ordinal);

            if (calls.Any(call => !ids.Contains(call.Id)))
            {
                return false;
            }
        }

        return true;
    }

    private static AiAssistantTurn Turn(string question, string answer, params AiAssistantStep[] steps) => new()
    {
        UserText = question,
        AnswerText = answer,
        Steps = [.. steps],
        Success = true,
    };

    private static AiAssistantStep Step(string callId, int round, string toolName, string argumentsJson, string resultText) => new()
    {
        CallId = callId,
        Round = round,
        ToolName = toolName,
        ParamSummary = toolName,
        ArgumentsJson = argumentsJson,
        ResultText = resultText,
        ResultSummary = resultText.Length <= 160 ? resultText : resultText[..160],
        Success = true,
    };

    private static async Task CustomPromptAsync(Check c)
    {
        using var server = new MockLlmServer((_, _) => MockLlmServer.Text("好的"));
        var turn = await new AiAssistantEngine().SendAsync(
            "x",
            Ctx(server, prompt: "你是探针专用助手"),
            CancellationToken.None);
        _ = await ReadFinalAsync(turn);

        var body = server.RequestBodies[0];
        c.That(ProbeJson.Contains(body, "你是探针专用助手"), "自定义提示词已下发");
        c.That(!ProbeJson.Contains(body, "严禁编造"), "自定义提示词时不再带内置规则（整体替换）");
    }

    private static async Task CancelAsync(Check c)
    {
        using var server = new MockLlmServer((_, _) => MockLlmServer.ToolCall("c1", "probe_slow", "{}"));
        ProbeTools.ResetCounters();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
        var threw = false;
        try
        {
            await new AiAssistantEngine().SendAsync("慢工具", Ctx(server), cts.Token);
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }

        c.That(threw, "用户取消时 SendAsync 抛 OperationCanceledException（由界面侧处理为「已停止」）");
        c.Equal(ProbeTools.SlowCalls, 1, "慢工具确实被启动过");
    }

    // ============================ 工具方法 ============================

    private static bool IsStreaming(string requestBody) => requestBody.Contains("\"stream\":true", StringComparison.Ordinal);

    private static AssistantContext Ctx(
        MockLlmServer server,
        IReadOnlyList<ChatMessage>? context = null,
        string? prompt = null)
        => new()
        {
            Context = context,
            SystemPrompt = prompt,
            Endpoint = new ChatEndpoint
            {
                Protocol = ChatProtocolFactory.OpenAi,
                BaseUrl = server.BaseUrl,
                ApiKey = "probe-key",
                Model = "probe-model",
                TimeoutSeconds = 30,
            },
            Tools = new ToolContext
            {
                Character = null,
                Characters = [],
                OnUiThread = action => action(),
                Progress = null,
            },
        };

    private static async Task<string> ReadFinalAsync(AiAssistantEngine.Turn turn, CancellationToken ct = default)
    {
        var builder = new StringBuilder();
        await foreach (var delta in turn.ReadFinalAsync(ct))
        {
            builder.Append(delta);
        }

        return builder.ToString();
    }
}
