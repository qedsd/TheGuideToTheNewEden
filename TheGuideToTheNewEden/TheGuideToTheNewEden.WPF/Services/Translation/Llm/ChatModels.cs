using System.Net;

namespace TheGuideToTheNewEden.WPF.Services.Translation.Llm;

/// <summary>对话消息角色。</summary>
public enum ChatRole
{
    System,
    User,
    Assistant,
}

/// <summary>一条对话消息（与具体协议无关）。</summary>
/// <param name="Role">角色。</param>
/// <param name="Content">文本内容。</param>
public sealed record ChatMessage(ChatRole Role, string Content)
{
    public static ChatMessage User(string content) => new(ChatRole.User, content);

    public static ChatMessage Assistant(string content) => new(ChatRole.Assistant, content);
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

    /// <summary>最大输出 token；&lt;= 0 表示不下发该字段（推理模型走服务端默认）。</summary>
    public int MaxTokens { get; init; } = 1024;

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

    /// <summary>原始响应（诊断用，可能很大，勿写日志）。</summary>
    public string? Raw { get; init; }
}

/// <summary>流式解析过程中的可变状态（每次请求一个实例，协议实现是单例但无状态）。</summary>
public sealed class ChatStreamState
{
    /// <summary>是否收到结束标记。</summary>
    public bool Done { get; set; }

    /// <summary>累计追加的文本（用于兜底：某些协议只在结束时给全文）。</summary>
    public System.Text.StringBuilder Text { get; } = new();

    public ChatUsage Usage { get; set; } = ChatUsage.Empty;

    public string? FinishReason { get; set; }
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
