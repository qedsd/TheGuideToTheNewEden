using System.Net;
using System.Text.Json.Nodes;

namespace TheGuideToTheNewEden.WPF.Services.Translation.Llm;

/// <summary>对话消息角色。</summary>
public enum ChatRole
{
    System,
    User,
    Assistant,

    /// <summary>工具执行结果（OpenAI <c>tool</c> 消息 / Anthropic <c>tool_result</c> / Gemini <c>functionResponse</c> 的归一化）。</summary>
    Tool,
}

/// <summary>
/// 模型请求携带的一次工具声明（发给模型）。
/// <para>
/// 各协议在 <see cref="IChatProtocol.BuildRequest"/> 里把 <see cref="ChatRequest.Tools"/> 转成自己的形状
/// （OpenAI <c>tools.function</c> / Anthropic <c>tools.input_schema</c> / Gemini <c>tools.functionDeclarations</c>）。
/// </para>
/// </summary>
public sealed class ChatTool
{
    /// <summary>英文工具名（面向 LLM，如 <c>market_orders</c>）。</summary>
    public required string Name { get; init; }

    /// <summary>何时用、参数、返回、已知限制（面向 LLM 的英文描述）。</summary>
    public required string Description { get; init; }

    /// <summary>参数 JSON Schema（object 型）；为空时各协议按自己的最小形状兜底。</summary>
    public JsonObject? Parameters { get; init; }
}

/// <summary>模型返回的一次工具调用（归一化；各协议形状在适配器里转换）。</summary>
public sealed record ChatToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>一条对话消息（与具体协议无关）。</summary>
/// <param name="Role">角色。</param>
/// <param name="Content">文本内容（assistant 带工具调用时可为 null）。</param>
public sealed record ChatMessage(ChatRole Role, string? Content)
{
    /// <summary>Role=<see cref="ChatRole.Tool"/> 时必填：对应的工具调用 ID（OpenAI id / Anthropic tool_use id / Gemini 生成的 fc-N）。</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Role=<see cref="ChatRole.Tool"/> 时携带的工具名（Gemini 的 functionResponse 按名字配对，不能依赖顺序）。</summary>
    public string? ToolName { get; init; }

    /// <summary>Role=<see cref="ChatRole.Assistant"/> 且模型要求调用工具时携带（各协议回传时用）。</summary>
    public IReadOnlyList<ChatToolCall>? ToolCalls { get; init; }

    public static ChatMessage User(string content) => new(ChatRole.User, content);

    public static ChatMessage Assistant(string content) => new(ChatRole.Assistant, content);

    public static ChatMessage Tool(string toolCallId, string content, string? toolName = null)
        => new(ChatRole.Tool, content) { ToolCallId = toolCallId, ToolName = toolName };

    public static ChatMessage AssistantToolCalls(IReadOnlyList<ChatToolCall> calls) => new(ChatRole.Assistant, null) { ToolCalls = calls };
}

/// <summary>
/// 一个模型端点（与协议无关的连接与生成参数）。
/// <see cref="Protocol"/> 决定用哪个 <see cref="IChatProtocol"/> 适配器。
/// </summary>
public sealed class ChatEndpoint
{
    /// <summary>协议标识，见 <see cref="ChatProtocolFactory"/>。</summary>
    public string Protocol { get; init; } = ChatProtocolFactory.OpenAi;

    /// <summary>服务地址（可为裸域名、带版本路径的地址，或完整的 chat/completions 地址）。</summary>
    public string BaseUrl { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    /// <summary>模型名（Azure 下是部署名）。</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Azure OpenAI 的 api-version（其他协议忽略）。</summary>
    public string ApiVersion { get; init; } = string.Empty;

    /// <summary>采样温度；部分推理模型不支持，置为 0 时不下发该字段。</summary>
    public double Temperature { get; init; } = 0.2;

    /// <summary>
    /// 最大输出 token；<b>0 = 不下发该字段</b>（交给服务端默认）。阶段 121 起默认 0——与
    /// <c>AiModelSettings.MaxTokens</c> 保持一致（两处默认值不同曾是个陷阱：直接 new 端点会静默拿到 1024；
    /// 阶段 125 起模型端点类从 <c>AiTranslationSettings</c> 拆成了 <c>AiModelSettings</c>）。
    /// Anthropic 例外：它的 API 必填 <c>max_tokens</c>，0 时由适配器回退到自己的 <c>FallbackMaxTokens</c>。
    /// </summary>
    public int MaxTokens { get; init; } = 0;

    /// <summary>单次请求超时（秒），覆盖首个字节到最后一个字节。</summary>
    public int TimeoutSeconds { get; init; } = 60;

    /// <summary>
    /// 思考模式（见 <see cref="ThinkingModes"/>）：<c>auto</c> 不下发；其余由支持它的协议（OpenAI 兼容里的 DeepSeek 系）
    /// 写进请求体。<b>是否值得下发由设置层判断</b>（非 DeepSeek 端点一律是 <c>auto</c>，避免不认识该字段的服务商 400）。
    /// </summary>
    public string ThinkingMode { get; init; } = ThinkingModes.Auto;

    /// <summary>是否已配置到可用的程度（协议无关的最小校验）。</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(Model);

    public ChatEndpoint Clone() => new()
    {
        Protocol = Protocol,
        BaseUrl = BaseUrl,
        ApiKey = ApiKey,
        Model = Model,
        ApiVersion = ApiVersion,
        Temperature = Temperature,
        MaxTokens = MaxTokens,
        TimeoutSeconds = TimeoutSeconds,
        ThinkingMode = ThinkingMode,
    };
}

/// <summary>一次对话请求。</summary>
public sealed class ChatRequest
{
    public required ChatEndpoint Endpoint { get; init; }

    /// <summary>系统提示（各协议放置位置不同，由适配器负责）。</summary>
    public string? SystemPrompt { get; init; }

    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    /// <summary>是否流式返回。</summary>
    public bool Stream { get; init; }

    /// <summary>要求返回 JSON 对象（不支持该能力的协议会忽略）。</summary>
    public bool JsonMode { get; init; }

    /// <summary>工具声明（发给模型；各协议在 BuildRequest 里写进自己的形状）。</summary>
    public IReadOnlyList<ChatTool>? Tools { get; init; }
}

/// <summary>token 用量（各协议字段名不同，由适配器归一化）。</summary>
public sealed record ChatUsage(int InputTokens, int OutputTokens)
{
    public static readonly ChatUsage Empty = new(0, 0);

    public int TotalTokens => InputTokens + OutputTokens;

    public bool IsEmpty => InputTokens == 0 && OutputTokens == 0;
}

/// <summary>一次对话的结果（归一化）。</summary>
public sealed class ChatCompletion
{
    public required string Text { get; init; }

    public ChatUsage Usage { get; init; } = ChatUsage.Empty;

    /// <summary>结束原因（各协议原样或归一化，仅供诊断）。</summary>
    public string? FinishReason { get; init; }

    /// <summary>模型要求调用的工具（非流式解析；没有为 null）。</summary>
    public IReadOnlyList<ChatToolCall>? ToolCalls { get; init; }

    /// <summary>原始响应（诊断用，可能很大，勿写日志）。</summary>
    public string? Raw { get; init; }
}

/// <summary>流式解析过程中的可变状态（每次请求一个实例，协议实现是单例但无状态）。</summary>
public sealed class ChatStreamState
{
    /// <summary>流式工具调用的增量累积器（OpenAI 按 index / Anthropic 按 content block index / Gemini 按出现序）。</summary>
    internal sealed class ToolCallAccumulator
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public System.Text.StringBuilder Arguments = new();
    }

    private readonly Dictionary<int, ToolCallAccumulator> _toolCalls = [];
    private IReadOnlyList<ChatToolCall>? _finalizedToolCalls;

    /// <summary>是否收到结束标记。</summary>
    public bool Done { get; set; }

    /// <summary>累计追加的文本（用于兜底：某些协议只在结束时给全文）。</summary>
    public System.Text.StringBuilder Text { get; } = new();

    public ChatUsage Usage { get; set; } = ChatUsage.Empty;

    public string? FinishReason { get; set; }

    /// <summary>流式过程中累积到的工具调用（调用方在流结束后读取；没有为 null）。</summary>
    public IReadOnlyList<ChatToolCall>? ToolCalls => _finalizedToolCalls;

    /// <summary>取（或创建）指定 index 的工具调用累积器（供适配器的 ParseStreamLine 用）。</summary>
    internal ToolCallAccumulator EnsureToolCall(int index)
    {
        if (!_toolCalls.TryGetValue(index, out var acc))
        {
            acc = new ToolCallAccumulator();
            _toolCalls[index] = acc;
        }

        return acc;
    }

    /// <summary>把累积器定稿成 <see cref="ToolCalls"/>（在 [DONE] / message_stop / finishReason 分支调用；幂等）。</summary>
    internal void FinalizeToolCalls()
    {
        if (_finalizedToolCalls is not null)
        {
            return;
        }

        if (_toolCalls.Count == 0)
        {
            _finalizedToolCalls = [];
            return;
        }

        var list = new List<ChatToolCall>();
        foreach (var pair in _toolCalls.OrderBy(p => p.Key))
        {
            var acc = pair.Value;
            list.Add(new ChatToolCall(acc.Id, acc.Name, acc.Arguments.Length == 0 ? "{}" : acc.Arguments.ToString()));
        }

        _finalizedToolCalls = list;
    }

    // ---------- Gemini 流式专用（每 chunk 重复全量 parts，按名字去重，避免重复累积） ----------

    private readonly HashSet<string> _seenFunctionCalls = new(StringComparer.Ordinal);

    /// <summary>已见过的 functionCall 名（Gemini 流式重复全量 parts 时去重）。</summary>
    internal HashSet<string> SeenFunctionCalls => _seenFunctionCalls;

    /// <summary>functionCall 出现的顺序计数（生成 fc-N id）。</summary>
    internal int FunctionCallCount { get; set; }

    // ---------- DeepSeek DSML 兼容（阶段 120） ----------

    private readonly System.Text.StringBuilder _pendingText = new();

    /// <summary>
    /// 流里出现过 DSML 工具调用标记（<c>&lt;｜DSML｜</c>）。一旦为 true，就不再向界面吐原始文本
    /// （调用方在流结束后据此给一句明确提示，而不是把一段 XML 当回答显示）。
    /// </summary>
    public bool DsmlSeen { get; private set; }

    /// <summary>被扣下的原始文本（诊断/解析用；正常流式结束后为空）。</summary>
    public string DsmlRaw => _pendingText.ToString();

    /// <summary>DSML 哨兵及其前缀（结尾是半个哨兵时要先扣住，等下一块再判断）。</summary>
    private static readonly string[] SentinelPrefixes = BuildSentinelPrefixes();

    private static string[] BuildSentinelPrefixes()
    {
        const string sentinel = "<\uFF5CDSML\uFF5C";
        const string asciiSentinel = "<|DSML|";
        var list = new List<string>();
        for (var i = 1; i <= sentinel.Length; i++)
        {
            list.Add(sentinel[..i]);
        }

        for (var i = 1; i <= asciiSentinel.Length; i++)
        {
            list.Add(asciiSentinel[..i]);
        }

        return [.. list];
    }

    /// <summary>
    /// 把流式增量过一遍 DSML 过滤：返回可展示文本；返回 null 表示"这次扣住不给界面"
    /// （要么已经进了 DSML 块，要么结尾可能是哨兵的前缀、要等下一块）。
    /// </summary>
    internal string? FilterDelta(string delta)
    {
        if (string.IsNullOrEmpty(delta))
        {
            return null;
        }

        _pendingText.Append(delta);

        if (DsmlSeen)
        {
            return null;
        }

        var buffered = _pendingText.ToString();
        if (DeepSeekDsmlParser.ContainsDsml(buffered))
        {
            DsmlSeen = true;
            return null;
        }

        if (SentinelPrefixes.Any(p => buffered.EndsWith(p, StringComparison.Ordinal)))
        {
            return null;   // 可能是被切开的哨兵，先扣着
        }

        _pendingText.Clear();
        return buffered;
    }

    /// <summary>流结束时把扣住的文本交出来（没有则 null）。已进入 DSML 块时**保留缓冲**供诊断，不再交给界面。</summary>
    internal string? FlushPending()
    {
        if (DsmlSeen)
        {
            return null;   // 留着给 <see cref="DsmlRaw"/> 与引擎的提示用；内容只有这一条响应的标记文本
        }

        if (_pendingText.Length == 0)
        {
            return null;
        }

        var text = _pendingText.ToString();
        _pendingText.Clear();
        return text;
    }
}

/// <summary>调用大模型失败（已归一化为可直接展示的信息）。</summary>
public sealed class ChatException : Exception
{
    public ChatException(string message, HttpStatusCode? statusCode = null, string? responseBody = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode? StatusCode { get; }

    public string? ResponseBody { get; }
}
