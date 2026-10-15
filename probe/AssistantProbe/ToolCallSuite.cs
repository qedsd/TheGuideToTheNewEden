using System.Text.Json.Nodes;
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
    }

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
