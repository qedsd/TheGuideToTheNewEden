namespace TheGuideToTheNewEden.WPF.Services.Translation.Llm;

/// <summary>
/// 协议注册表：把设置里的协议标识映射到适配器。
/// <para>
/// 刻意不做 Ollama 原生协议（用户要求）：本地模型走 Ollama / LM Studio / vLLM 的
/// **OpenAI 兼容端点**即可，用 <see cref="OpenAi"/> 一个协议覆盖。
/// 需要 Bedrock / Vertex 这类需要 SigV4 或 OAuth2 签名的服务时，建议在前面挂一层
/// one-api / new-api / LiteLLM 网关转成 OpenAI 协议，而不是在应用里实现签名。
/// </para>
/// </summary>
public static class ChatProtocolFactory
{
    public const string OpenAi = "openai";
    public const string AzureOpenAi = "azure-openai";
    public const string Anthropic = "anthropic";
    public const string Gemini = "gemini";

    private static readonly IChatProtocol[] RegisteredProtocols =
    [
        new OpenAiChatProtocol(),
        new AzureOpenAiChatProtocol(),
        new AnthropicChatProtocol(),
        new GeminiChatProtocol(),
    ];

    /// <summary>全部已注册协议（顺序即界面下拉顺序，默认第一项）。</summary>
    public static IReadOnlyList<IChatProtocol> All => RegisteredProtocols;

    /// <summary>按标识取协议；找不到时退回 OpenAI 兼容（设置里的旧值被删掉时不至于让功能不可用）。</summary>
    public static IChatProtocol Get(string? key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            var matched = RegisteredProtocols.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.Ordinal));
            if (matched is not null)
            {
                return matched;
            }
        }

        return RegisteredProtocols[0];
    }
}
