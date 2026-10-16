using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.Services.Translation;
using TheGuideToTheNewEden.WPF.Services.Translation.Llm;

namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// 协议层工具调用形状（**完全离线**，不联网）：tools 声明形状、消息往返形状、tool_calls 解析、流式累积、以及
/// **回归红线**——不带 tools 的请求体不得出现 tools/tool_choice（阶段 105 定的基线）。
/// </summary>
internal static class ToolCallSuite
{
    /// <summary>构造一个"形状正确但不会真的联网"的端点（协议可换）。</summary>
    private static ChatEndpoint Ep(string protocol = ChatProtocolFactory.OpenAi) => new()
    {
        Protocol = protocol,
        BaseUrl = "http://127.0.0.1:9/v1",
        ApiKey = "probe-key",
        Model = "probe-model",
    };

    private static ChatTool Tool(string name = "market_orders") => new()
    {
        Name = name,
        Description = "查询市场订单",
        Parameters = JsonNode.Parse(
            """{"type":"object","properties":{"itemName":{"type":"string"}},"required":["itemName"]}""")!.AsObject(),
    };

    public static void Run(Check c)
    {
        c.Section("ToolCallSuite · OpenAI 兼容（DeepSeek/通义/GLM/Kimi/OpenRouter 同款）");
        OpenAi(c);

        c.Section("ToolCallSuite · Anthropic");
        Anthropic(c);

        c.Section("ToolCallSuite · Gemini");
        Gemini(c);

        c.Section("ToolCallSuite · 回归红线（不带 tools）");
        Regression(c);

        c.Section("ToolCallSuite · DeepSeek DSML 兼容（阶段 120）");
        Dsml(c);

        c.Section("ToolCallSuite · 阶段 121：最大输出 token 默认 0（不下发）");
        MaxTokensDefault(c);
    }

    /// <summary>
    /// 阶段 121：`AiModelSettings.MaxTokens` 默认改成 0 = **不下发**（交给服务端默认）。
    /// 阶段 125：模型端点从 AiTranslationSettings 拆成独立的 AiModelSettings（助手与翻译共用）。
    /// 原先默认 1024 会把工具调用 JSON / Markdown 答案截断（阶段 120 的 DSML 泄漏就与截断叠加过）。
    /// </summary>
    private static void MaxTokensDefault(Check c)
    {
        c.Equal(new AiModelSettings().MaxTokens, 0, "AiModelSettings 默认 MaxTokens = 0");

        var protocol = new OpenAiChatProtocol();
        var noCap = Json(Raw(protocol, new ChatRequest
        {
            Endpoint = Ep(),
            Messages = [ChatMessage.User("x")],
        }));
        c.That(!noCap.ContainsKey("max_tokens"), "MaxTokens = 0 时请求体不写 max_tokens（服务端默认）");

        var withCap = Json(Raw(protocol, new ChatRequest
        {
            Endpoint = new ChatEndpoint
            {
                Protocol = ChatProtocolFactory.OpenAi,
                BaseUrl = "http://127.0.0.1:9/v1",
                ApiKey = "probe-key",
                Model = "probe-model",
                MaxTokens = 4096,
            },
            Messages = [ChatMessage.User("x")],
        }));
        c.Equal(withCap["max_tokens"]!.GetValue<int>(), 4096, "显式设了非 0 才下发 max_tokens");
    }

    // ============================ DeepSeek DSML ============================

    /// <summary>
    /// DeepSeek V3.2/V4 的原生工具调用标记（DSML）会**漏进 content**（服务端已知问题），
    /// 客户端必须能兼容解析、并且**绝不把标记当回答显示**。这里覆盖：语法（两种块名/竖线变体/类型语义/
    /// 直接 JSON/多调用/截断）、ParseResponse 集成、流式拦截、以及工具名宽松匹配。
    /// </summary>
    private static void Dsml(Check c)
    {
        // ① 真实形状（用户实机截图里那一轮：deepseek-flash 把 map_system_info 写成了 mapsysteminfo）
        var real = "<\uFF5CDSML\uFF5Ctool_calls>\n"
                   + "<\uFF5CDSML\uFF5Cinvoke name=\"mapsysteminfo\">\n"
                   + "<\uFF5CDSML\uFF5Cparameter name=\"systemName\" string=\"true\">1DQ1-A</\uFF5CDSML\uFF5Cparameter>\n"
                   + "</\uFF5CDSML\uFF5Cinvoke>\n"
                   + "</\uFF5CDSML\uFF5Ctool_calls>";
        c.That(DeepSeekDsmlParser.ContainsDsml(real), "识别 DSML 哨兵（全角竖线）");
        var parsed = DeepSeekDsmlParser.Parse(real);
        c.That(parsed.Detected, "真实形状：Detected");
        c.Equal(parsed.Calls.Count, 1, "真实形状：解析出 1 个调用");
        c.Equal(parsed.Calls[0].Name, "mapsysteminfo", "真实形状：调用名（模型没写下划线）");
        c.That(parsed.Calls[0].ArgumentsJson.Contains("1DQ1-A"), "真实形状：参数值");
        c.Equal(parsed.Text, "", "真实形状：正文里不再残留标记");

        // ② V3.2 的块名是 function_calls；ASCII 单竖线也要认
        var v32 = "<|DSML|function_calls><|DSML|invoke name=\"translate\"><|DSML|parameter name=\"text\" string=\"true\">hi</|DSML|parameter></|DSML|invoke></|DSML|function_calls>";
        var v32Parsed = DeepSeekDsmlParser.Parse(v32);
        c.Equal(v32Parsed.Calls.Count, 1, "V3.2 块名 + ASCII 竖线可解析");
        c.Equal(v32Parsed.Calls[0].Name, "translate", "V3.2 调用名");

        // ③ 类型语义：string="false" 按 JSON 解析，缺 string 属性的当字符串
        var typed = "<\uFF5CDSML\uFF5Ctool_calls><\uFF5CDSML\uFF5Cinvoke name=\"t\">"
                    + "<\uFF5CDSML\uFF5Cparameter name=\"n\" string=\"false\">123</\uFF5CDSML\uFF5Cparameter>"
                    + "<\uFF5CDSML\uFF5Cparameter name=\"b\" string=\"false\">true</\uFF5CDSML\uFF5Cparameter>"
                    + "<\uFF5CDSML\uFF5Cparameter name=\"arr\" string=\"false\">[1,2]</\uFF5CDSML\uFF5Cparameter>"
                    + "<\uFF5CDSML\uFF5Cparameter name=\"s\" string=\"true\">456</\uFF5CDSML\uFF5Cparameter>"
                    + "<\uFF5CDSML\uFF5Cparameter name=\"bare\">text</\uFF5CDSML\uFF5Cparameter>"
                    + "</\uFF5CDSML\uFF5Cinvoke></\uFF5CDSML\uFF5Ctool_calls>";
        var args = JsonNode.Parse(DeepSeekDsmlParser.Parse(typed).Calls[0].ArgumentsJson)!.AsObject();
        c.Equal(args["n"]!.GetValue<int>(), 123, "string=false 的数字解析成 number");
        c.Equal(args["b"]!.GetValue<bool>(), true, "string=false 的布尔解析成 bool");
        c.Equal(args["arr"]!.AsArray().Count, 2, "string=false 的数组解析成 array");
        c.Equal(args["s"]!.GetValue<string>(), "456", "string=true 的数字保持字符串");
        c.Equal(args["bare"]!.GetValue<string>(), "text", "缺 string 属性按字符串");

        // ④ 一个块里多个 invoke；invoke 内直接写 JSON 也支持
        var multi = "<\uFF5CDSML\uFF5Ctool_calls>"
                    + "<\uFF5CDSML\uFF5Cinvoke name=\"a\"><\uFF5CDSML\uFF5Cparameter name=\"x\" string=\"true\">1</\uFF5CDSML\uFF5Cparameter></\uFF5CDSML\uFF5Cinvoke>"
                    + "<\uFF5CDSML\uFF5Cinvoke name=\"b\">{ \"y\": 2 }</\uFF5CDSML\uFF5Cinvoke>"
                    + "</\uFF5CDSML\uFF5Ctool_calls>";
        var multiParsed = DeepSeekDsmlParser.Parse(multi);
        c.Equal(multiParsed.Calls.Count, 2, "一个块里多个 invoke");
        c.Equal(multiParsed.Calls[1].ArgumentsJson.Replace(" ", ""), """{"y":2}""", "invoke 内直接写 JSON");
        c.That(multiParsed.Calls[0].Id != multiParsed.Calls[1].Id, "各调用 id 唯一（回填 tool 消息要用）");

        // ⑤ 正文保留、句末标记剔除
        var mixed = "前面的话 <\uFF5CDSML\uFF5Ctool_calls><\uFF5CDSML\uFF5Cinvoke name=\"a\"></\uFF5CDSML\uFF5Cinvoke></\uFF5CDSML\uFF5Ctool_calls> 后面的话<\uFF5Cend\u2581of\u2581sentence\uFF5C>";
        var mixedParsed = DeepSeekDsmlParser.Parse(mixed);
        c.That(mixedParsed.Text.Contains("前面的话") && mixedParsed.Text.Contains("后面的话"), "块前后的正文都保留");
        c.That(!mixedParsed.Text.Contains("end\u2581of\u2581sentence"), "句末标记被剔除");

        // ⑥ 回归：没有标记的普通回答原样返回
        var plain = DeepSeekDsmlParser.Parse("就是一段普通回答，带 <html> 标签也不算 DSML。");
        c.That(!plain.Detected && plain.Calls.Count == 0, "普通回答：Detected=false");
        c.Equal(plain.Text, "就是一段普通回答，带 <html> 标签也不算 DSML。", "普通回答原样返回");

        // ⑦ 截断：只有开头、没有闭合标签 —— 能解析就解析，绝不把标记显示出来
        var truncated = DeepSeekDsmlParser.Parse(
            "<\uFF5CDSML\uFF5Ctool_calls><\uFF5CDSML\uFF5Cinvoke name=\"a\"><\uFF5CDSML\uFF5Cparameter name=\"x\" string=\"true\">1");
        c.Equal(truncated.Calls.Count, 1, "截断时尽力解析出调用");
        c.That(truncated.Truncated, "截断被标记");
        c.That(!truncated.Text.Contains("DSML"), "截断时正文里也不留标记");

        var empty = DeepSeekDsmlParser.Parse("<\uFF5CDSML\uFF5Ctool_calls>");
        c.That(empty.Detected && empty.Calls.Count == 0, "空块：Detected 但无调用");
        c.That(!string.IsNullOrWhiteSpace(empty.Error), "空块给出可照做的错误说明");
        c.That(!empty.Text.Contains("DSML"), "空块的正文里不留标记");

        // ⑧ ParseResponse 集成：非流式响应里 content 是 DSML → 当成工具调用
        var protocol = new OpenAiChatProtocol();
        var canned = new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject { ["message"] = new JsonObject { ["content"] = real } },
            },
            ["usage"] = new JsonObject { ["prompt_tokens"] = 11597, ["completion_tokens"] = 89 },
        }.ToJsonString();
        var completion = protocol.ParseResponse(canned);
        c.Equal(completion.ToolCalls?.Count ?? 0, 1, "ParseResponse：DSML 内容被当成工具调用");
        c.Equal(completion.ToolCalls![0].Name, "mapsysteminfo", "ParseResponse：调用名带出来");
        c.That(!(completion.Text ?? "").Contains("DSML"), "ParseResponse：正文里不再有标记");

        // 有标准 tool_calls 时不走 DSML 分支（避免双重解析）
        var structured = """{"choices":[{"message":{"content":"x","tool_calls":[{"id":"c1","function":{"name":"market_orders","arguments":"{}"}}]}}]}""";
        var structuredCompletion = protocol.ParseResponse(structured);
        c.Equal(structuredCompletion.ToolCalls?[0].Name, "market_orders", "有标准 tool_calls 时优先用它");

        // ⑨ 流式拦截：哨兵被切开也不吐出半截标签
        var state = new ChatStreamState();
        var emitted = new List<string>();
        foreach (var line in new[]
                 {
                     DeltaJson("好"),
                     DeltaJson("的"),
                     DeltaJson("<"),
                     DeltaJson("\uFF5CDSML\uFF5Ctool_calls>"),
                     DeltaJson("invoke name=\"a\""),
                     "data: [DONE]",
                 })
        {
            var delta = protocol.ParseStreamLine(line, state);
            if (!string.IsNullOrEmpty(delta))
            {
                emitted.Add(delta);
            }
        }

        c.Equal(emitted.Count, 2, "流式：哨兵前的正常增量照常吐出（逐块）");
        c.Equal(string.Concat(emitted), "好的", "流式：正常文本内容正确");
        c.That(state.DsmlSeen, "流式：检测到 DSML 后置位（引擎据此给提示）");
        c.That(state.DsmlRaw.Contains("invoke name"), "流式：原始文本被扣下供诊断");
        c.That(!state.Text.ToString().Contains("DSML"), "流式：累计文本里没有标记");

        // ⑩ 流式回归：结尾恰好是"半个哨兵前缀"时先扣住，确认不是哨兵后要补吐出来
        var state2 = new ChatStreamState();
        var first = protocol.ParseStreamLine(DeltaJson("结论<"), state2);
        var second = protocol.ParseStreamLine(DeltaJson("不是标记"), state2);
        c.That(string.IsNullOrEmpty(first), "流式：疑似哨兵前缀先扣住");
        c.Equal(second, "结论<不是标记", "流式：确认不是哨兵后补吐（不丢字）");

        // ⑪ 工具名宽松匹配：DSML 里常见 mapsysteminfo（模型不写下划线）
        c.Equal(ToolRegistry.FindLoose("mapsysteminfo")?.Name, "map_system_info", "宽松匹配 mapsysteminfo");
        c.Equal(ToolRegistry.FindLoose("map_system_info")?.Name, "map_system_info", "精确名不受影响");
        c.That(ToolRegistry.FindLoose("GetCharactersCharacterIdWallet") is null, "ESI 的 operationId 不会被当成工具名");
        c.That(ToolRegistry.FindLoose("nosuchtoolatall") is null, "完全没影的名字返回 null");
    }

    /// <summary>构造一行 SSE 增量（OpenAI 兼容形状），避免手写 JSON 转义出错。</summary>
    private static string DeltaJson(string content)
        => new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject { ["delta"] = new JsonObject { ["content"] = content } },
            },
        }.ToJsonString();

    // ============================ OpenAI 兼容 ============================

    private static void OpenAi(Check c)
    {
        var protocol = new OpenAiChatProtocol();

        var withTools = Json(Raw(protocol, new ChatRequest
        {
            Endpoint = Ep(),
            Messages = [ChatMessage.User("巨神兵级多少钱")],
            Tools = [Tool()],
        }));

        c.That(withTools["tools"] is JsonArray { Count: 1 }, "请求体含 1 个 tools");
        c.Equal(withTools["tools"]![0]!["type"]!.GetValue<string>(), "function", "tools[0].type");
        c.Equal(withTools["tools"]![0]!["function"]!["name"]!.GetValue<string>(), "market_orders", "tools[0].function.name");
        c.Equal(withTools["tools"]![0]!["function"]!["parameters"]!["type"]!.GetValue<string>(), "object", "parameters 是 object 型 schema");
        c.Equal(withTools["tools"]![0]!["function"]!["parameters"]!["required"]![0]!.GetValue<string>(), "itemName", "parameters.required 透传");
        c.Equal(withTools["tool_choice"]!.GetValue<string>(), "auto", "tool_choice=auto");
        c.That(withTools["stream"]!.GetValue<bool>() == false, "工具轮是非流式（stream=false）");

        // 无 Parameters 的工具要有兜底 schema（否则部分网关 400）
        var noSchema = Json(Raw(protocol, new ChatRequest
        {
            Endpoint = Ep(),
            Messages = [ChatMessage.User("x")],
            Tools = [new ChatTool { Name = "probe_echo", Description = "d" }],
        }));
        c.Equal(noSchema["tools"]![0]!["function"]!["parameters"]!["type"]!.GetValue<string>(), "object", "工具无 schema 时兜底成 object");

        // 消息往返：assistant(tool_calls) + tool 结果
        var roundTrip = Json(Raw(protocol, new ChatRequest
        {
            Endpoint = Ep(),
            Messages =
            [
                ChatMessage.User("巨神兵级多少钱"),
                ChatMessage.AssistantToolCalls([new ChatToolCall("call_1", "market_orders", """{"itemName":"Rifter"}""")]),
                ChatMessage.Tool("call_1", "卖单 1,300,000", "market_orders"),
                ChatMessage.User("那历史呢"),
            ],
        }));

        var messages = roundTrip["messages"]!.AsArray();
        c.Equal(messages.Count, 4, "消息条数（user / assistant(tool_calls) / tool / user）");
        c.Equal(messages[1]!["role"]!.GetValue<string>(), "assistant", "assistant 消息角色");
        c.That(messages[1]!["content"] is null, "带工具调用的 assistant 消息 content 为 null");
        c.Equal(messages[1]!["tool_calls"]![0]!["id"]!.GetValue<string>(), "call_1", "assistant 回传 tool_call id");
        c.That(messages[1]!["tool_calls"]![0]!["function"]!["arguments"]!.GetValue<string>().Contains("Rifter"), "assistant 回传 arguments 原样");
        c.Equal(messages[2]!["role"]!.GetValue<string>(), "tool", "工具结果角色 = tool");
        c.Equal(messages[2]!["tool_call_id"]!.GetValue<string>(), "call_1", "工具结果带 tool_call_id");

        // 解析（非流式）
        var canned = """
            {"choices":[{"message":{"role":"assistant","content":null,
              "tool_calls":[{"id":"call_1","type":"function","function":{"name":"market_orders","arguments":"{\"itemName\":\"Rifter\"}"}}]},
              "finish_reason":"tool_calls"}],"usage":{"prompt_tokens":20,"completion_tokens":9}}
            """;
        var calls = protocol.ParseToolCalls(canned);
        c.Equal(calls?.Count ?? 0, 1, "ParseToolCalls 解析出 1 个调用");
        c.Equal(calls![0].Id, "call_1", "解析 id");
        c.Equal(calls[0].Name, "market_orders", "解析 name");
        c.That(calls[0].ArgumentsJson.Contains("Rifter"), "解析 arguments");
        c.Equal(protocol.ParseResponse(canned).ToolCalls?.Count ?? 0, 1, "ParseResponse 同时带出 ToolCalls");
        c.That(protocol.ParseToolCalls("""{"choices":[{"message":{"content":"纯文本"}}]}""") is null, "纯文本响应解析出 null");
        c.That(protocol.ParseToolCalls("""{"choices":[{"message":{"tool_calls":[]}}]}""") is null, "空 tool_calls 数组解析出 null");
        var malformed = protocol.ParseToolCalls("""{"choices":[{"message":{"tool_calls":[{"id":"c1","function":{"name":"f","arguments":"{不是JSON"}}]}}]}""");
        c.Equal(malformed?[0].ArgumentsJson, "{不是JSON", "非法 arguments 原样保留（由引擎侧容错）");

        // 流式累积：name 与 arguments 都是分片、两个 index
        var state = new ChatStreamState();
        foreach (var line in new[]
                 {
                     """data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_a","type":"function","function":{"name":"market_","arguments":"{\"item"}}]}}]}""",
                     """data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"name":"orders","arguments":"Name\":\"Rifter\"}"}}]}}]}""",
                     """data: {"choices":[{"delta":{"tool_calls":[{"index":1,"id":"call_b","function":{"name":"translate","arguments":"{\"text\":\"hi\"}"}}]}}]}""",
                     """data: {"choices":[{"delta":{},"finish_reason":"tool_calls"}]}""",
                     "data: [DONE]",
                 })
        {
            protocol.ParseStreamLine(line, state);
        }

        c.Equal(state.ToolCalls?.Count ?? 0, 2, "流式累积出 2 个工具调用（按 index）");
        c.Equal(state.ToolCalls![0].Id, "call_a", "流式 id");
        c.Equal(state.ToolCalls[0].Name, "market_orders", "流式 name 分片拼接");
        c.Equal(state.ToolCalls[0].ArgumentsJson, """{"itemName":"Rifter"}""", "流式 arguments 分片拼接");
        c.Equal(state.ToolCalls[1].Name, "translate", "第二个调用独立累积");
        c.That(state.Done, "[DONE] 置结束标记");
    }

    // ============================ Anthropic ============================

    private static void Anthropic(Check c)
    {
        var protocol = new AnthropicChatProtocol();

        var withTools = Json(Raw(protocol, new ChatRequest
        {
            Endpoint = Ep(ChatProtocolFactory.Anthropic),
            SystemPrompt = "系统提示",
            Messages = [ChatMessage.User("巨神兵级多少钱")],
            Tools = [Tool()],
        }));

        c.Equal(withTools["tools"]![0]!["name"]!.GetValue<string>(), "market_orders", "Anthropic tools[0].name");
        c.Equal(withTools["tools"]![0]!["input_schema"]!["type"]!.GetValue<string>(), "object", "Anthropic 用 input_schema");
        c.Equal(withTools["system"]!.GetValue<string>(), "系统提示", "system 是顶层字段");

        // 消息往返：assistant(tool_use×2) + 两条 tool 结果 → **合并成一条 user** 的两个 tool_result
        var roundTrip = Json(Raw(protocol, new ChatRequest
        {
            Endpoint = Ep(),
            Messages =
            [
                ChatMessage.User("问"),
                ChatMessage.AssistantToolCalls(
                [
                    new ChatToolCall("call_1", "market_orders", """{"itemName":"Rifter"}"""),
                    new ChatToolCall("call_2", "translate", """{"text":"hi"}"""),
                ]),
                ChatMessage.Tool("call_1", "结果一", "market_orders"),
                ChatMessage.Tool("call_2", "结果二", "translate"),
                ChatMessage.User("继续"),
            ],
        }));

        var messages = roundTrip["messages"]!.AsArray();
        c.Equal(messages.Count, 3, "Anthropic 消息条数（user / assistant / 合并后的 user）");
        c.Equal(messages[1]!["content"]![0]!["type"]!.GetValue<string>(), "tool_use", "assistant 用 tool_use 块");
        c.Equal(messages[1]!["content"]![0]!["id"]!.GetValue<string>(), "call_1", "tool_use id 原样回传");
        c.Equal(messages[1]!["content"]![0]!["input"]!["itemName"]!.GetValue<string>(), "Rifter", "tool_use input 是解析后的对象");
        c.Equal(messages[2]!["role"]!.GetValue<string>(), "user", "工具结果落在 user 消息里");
        c.Equal(messages[2]!["content"]!.AsArray().Count, 3, "两条 tool_result + 紧随的用户文本合并进同一条 user 消息");
        c.Equal(messages[2]!["content"]![0]!["type"]!.GetValue<string>(), "tool_result", "tool_result 块");
        c.Equal(messages[2]!["content"]![1]!["tool_use_id"]!.GetValue<string>(), "call_2", "第二条 tool_result 的 tool_use_id");
        c.Equal(messages[2]!["content"]![2]!["type"]!.GetValue<string>(), "text", "后续用户文本与工具结果同属一条 user 消息");
        c.Equal(messages[2]!["content"]![2]!["text"]!.GetValue<string>(), "继续", "后续用户文本内容正确");

        var canned = """
            {"content":[{"type":"text","text":"我来查一下"},
                        {"type":"tool_use","id":"toolu_1","name":"market_orders","input":{"itemName":"Rifter"}}],
             "stop_reason":"tool_use","usage":{"input_tokens":20,"output_tokens":9}}
            """;
        var calls = protocol.ParseToolCalls(canned);
        c.Equal(calls?.Count ?? 0, 1, "Anthropic ParseToolCalls");
        c.Equal(calls![0].Id, "toolu_1", "Anthropic 工具调用 id");
        c.Equal(calls[0].Name, "market_orders", "Anthropic 工具调用名");
        c.That(calls[0].ArgumentsJson.Contains("Rifter"), "Anthropic 工具调用参数");
        c.Equal(protocol.ParseResponse(canned).ToolCalls?.Count ?? 0, 1, "Anthropic ParseResponse 带出 ToolCalls");

        // 流式：content_block_start(tool_use) → input_json_delta ×2 → stop → message_stop
        var state = new ChatStreamState();
        foreach (var line in new[]
                 {
                     """data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"toolu_9","name":"market_orders","input":{}}}""",
                     """data: {"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"itemName\":"}}""",
                     """data: {"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"\"Rifter\"}"}}""",
                     """data: {"type":"content_block_stop","index":0}""",
                     """data: {"type":"message_stop"}""",
                 })
        {
            protocol.ParseStreamLine(line, state);
        }

        c.Equal(state.ToolCalls?.Count ?? 0, 1, "Anthropic 流式累积出 1 个工具调用");
        c.Equal(state.ToolCalls![0].Id, "toolu_9", "Anthropic 流式 id");
        c.Equal(state.ToolCalls[0].ArgumentsJson, """{"itemName":"Rifter"}""", "Anthropic 流式 partial_json 拼接");
    }

    // ============================ Gemini ============================

    private static void Gemini(Check c)
    {
        var protocol = new GeminiChatProtocol();

        var withTools = Json(Raw(protocol, new ChatRequest
        {
            Endpoint = Ep(ChatProtocolFactory.Gemini),
            Messages = [ChatMessage.User("巨神兵级多少钱")],
            Tools = [Tool()],
        }));

        c.Equal(withTools["tools"]![0]!["functionDeclarations"]![0]!["name"]!.GetValue<string>(), "market_orders", "Gemini functionDeclarations");
        c.Equal(withTools["tools"]![0]!["functionDeclarations"]![0]!["parameters"]!["type"]!.GetValue<string>(), "object", "Gemini parameters");

        // 消息往返：模型 functionCall + 用户 functionResponse（**按名配对**）
        var roundTrip = Json(Raw(protocol, new ChatRequest
        {
            Endpoint = Ep(),
            Messages =
            [
                ChatMessage.User("问"),
                ChatMessage.AssistantToolCalls(
                [
                    new ChatToolCall("fc-0", "market_orders", """{"itemName":"Rifter"}"""),
                    new ChatToolCall("fc-1", "translate", """{"text":"hi"}"""),
                ]),
                ChatMessage.Tool("fc-0", "结果一", "market_orders"),
                ChatMessage.Tool("fc-1", "结果二", "translate"),
            ],
        }));

        var contents = roundTrip["contents"]!.AsArray();
        c.Equal(contents.Count, 3, "Gemini contents 条数（user / model / 合并的 user）");
        c.Equal(contents[1]!["role"]!.GetValue<string>(), "model", "Gemini 模型角色 = model");
        c.Equal(contents[1]!["parts"]![0]!["functionCall"]!["name"]!.GetValue<string>(), "market_orders", "Gemini functionCall 名");
        c.Equal(contents[2]!["role"]!.GetValue<string>(), "user", "Gemini 工具结果在 user 里");
        c.Equal(contents[2]!["parts"]!.AsArray().Count, 2, "两条工具结果合并进同一条 user content");
        c.Equal(contents[2]!["parts"]![0]!["functionResponse"]!["name"]!.GetValue<string>(), "market_orders", "functionResponse 按 ToolName 配对（第 1 条）");
        c.Equal(contents[2]!["parts"]![1]!["functionResponse"]!["name"]!.GetValue<string>(), "translate", "functionResponse 按 ToolName 配对（第 2 条）");
        c.That(contents[2]!["parts"]![0]!["functionResponse"]!["response"]!["result"]!.GetValue<string>() == "结果一", "functionResponse 带结果文本");

        var canned = """
            {"candidates":[{"content":{"role":"model","parts":[{"functionCall":{"name":"market_orders","args":{"itemName":"Rifter"}}}]},
              "finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":20,"candidatesTokenCount":9}}
            """;
        var calls = protocol.ParseToolCalls(canned);
        c.Equal(calls?.Count ?? 0, 1, "Gemini ParseToolCalls");
        c.Equal(calls![0].Id, "fc-0", "Gemini 生成 fc-N id");
        c.Equal(calls[0].Name, "market_orders", "Gemini 工具名");
        c.That(calls[0].ArgumentsJson.Contains("Rifter"), "Gemini 工具参数");
        c.Equal(protocol.ParseResponse(canned).ToolCalls?.Count ?? 0, 1, "Gemini ParseResponse 带出 ToolCalls");

        // 流式：chunk 里带 functionCall（且重复出现时不得重复累积）
        var state = new ChatStreamState();
        protocol.ParseStreamLine("""data: {"candidates":[{"content":{"parts":[{"functionCall":{"name":"market_orders","args":{"itemName":"Rifter"}}}]}}]}""", state);
        protocol.ParseStreamLine("""data: {"candidates":[{"content":{"parts":[{"functionCall":{"name":"market_orders","args":{"itemName":"Rifter"}}}]},"finishReason":"STOP"}]}""", state);
        c.Equal(state.ToolCalls?.Count ?? 0, 1, "Gemini 流式重复 parts 不重复累积");
        c.Equal(state.ToolCalls![0].Name, "market_orders", "Gemini 流式工具名");
    }

    // ============================ 回归红线 ============================

    private static void Regression(Check c)
    {
        var plain = new ChatRequest
        {
            Endpoint = Ep(),
            SystemPrompt = "系统提示",
            Messages = [ChatMessage.User("Rifter"), ChatMessage.Assistant("裂谷级")],
        };

        foreach (var protocol in ChatProtocolFactory.All)
        {
            var body = Json(Raw(protocol, plain));
            c.That(!body.ContainsKey("tools"), $"{protocol.Key}：不带 tools 时不写 tools 字段（翻译回归红线）");
            c.That(!body.ContainsKey("tool_choice"), $"{protocol.Key}：不带 tools 时不写 tool_choice");

            var streaming = Json(Raw(protocol, new ChatRequest
            {
                Endpoint = Ep(),
                Messages = [ChatMessage.User("Rifter")],
                Stream = true,
            }));
            c.That(!streaming.ContainsKey("tools"), $"{protocol.Key}：流式请求也不写 tools");
        }

        // 空 tools 列表等价于不带
        var emptyTools = Json(Raw(new OpenAiChatProtocol(), new ChatRequest
        {
            Endpoint = Ep(),
            Messages = [ChatMessage.User("x")],
            Tools = [],
        }));
        c.That(!emptyTools.ContainsKey("tools"), "空 tools 列表不写 tools 字段");
    }

    // ============================ 工具方法 ============================

    private static string Raw(IChatProtocol protocol, ChatRequest request)
    {
        using var message = protocol.BuildRequest(request);
        return message.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
    }

    private static JsonObject Json(string raw) => JsonNode.Parse(raw)!.AsObject();
}
