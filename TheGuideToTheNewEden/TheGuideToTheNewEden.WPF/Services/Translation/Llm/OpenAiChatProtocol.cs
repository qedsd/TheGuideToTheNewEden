using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TheGuideToTheNewEden.WPF.Services.Translation.Llm;

/// <summary>
/// OpenAI 兼容协议（<c>POST {base}/v1/chat/completions</c>）。
/// 覆盖 OpenAI、DeepSeek、通义(DashScope 兼容模式)、智谱 GLM、Kimi、豆包 Ark、
/// OpenRouter、Groq、SiliconFlow、vLLM/SGLang/LM Studio 以及 one-api / new-api / LiteLLM 网关。
/// <para>
/// 地址按"用户可能填裸域名、带版本路径、或整条 chat/completions"三种情况归一化：
/// 已指向 chat/completions 就原样使用；只有裸域名才补 <c>/v1</c>；其余只补 <c>/chat/completions</c>
/// （这样 Ark 的 <c>/api/v3</c>、DashScope 的 <c>/compatible-mode/v1</c> 都不会被写坏）。
/// </para>
/// </summary>
public class OpenAiChatProtocol : IChatProtocol
{
    public virtual string Key => ChatProtocolFactory.OpenAi;

    public virtual string DisplayNameKey => "TranslationPage_Protocol_OpenAi";

    public virtual string DefaultBaseUrl => "https://api.openai.com/v1";

    public virtual string DefaultModel => "gpt-4o-mini";

    public virtual HttpRequestMessage BuildRequest(ChatRequest request)
    {
        var endpoint = request.Endpoint;
        var body = new JsonObject
        {
            ["model"] = endpoint.Model,
            ["messages"] = BuildMessages(request),
            ["stream"] = request.Stream,
        };

        // 置 0 表示"不下发"，给推理模型（不接受 temperature / 需要 max_completion_tokens）留出口
        if (endpoint.Temperature > 0)
        {
            body["temperature"] = endpoint.Temperature;
        }

        if (endpoint.MaxTokens > 0)
        {
            body["max_tokens"] = endpoint.MaxTokens;
        }

        if (request.JsonMode)
        {
            body["response_format"] = new JsonObject { ["type"] = "json_object" };
        }

        ApplyThinking(body, endpoint);

        if (request.Stream)
        {
            body["stream_options"] = new JsonObject { ["include_usage"] = true };
        }

        var message = new HttpRequestMessage(HttpMethod.Post, BuildUrl(endpoint.BaseUrl))
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        ApplyAuth(message, endpoint);
        return message;
    }

    protected virtual void ApplyAuth(HttpRequestMessage message, ChatEndpoint endpoint)
        => message.Headers.TryAddWithoutValidation("Authorization", $"Bearer {endpoint.ApiKey}");

    /// <summary>
    /// 思考模式参数（DeepSeek 系专有）：<c>thinking.type</c> + 可选 <c>reasoning_effort</c>。
    /// 是否该下发由设置层决定（非 DeepSeek 端点会是 <see cref="ThinkingModes.Auto"/>，这里就什么都不写）。
    /// </summary>
    protected static void ApplyThinking(JsonObject body, ChatEndpoint endpoint)
    {
        switch (endpoint.ThinkingMode)
        {
            case ThinkingModes.Off:
                body["thinking"] = new JsonObject { ["type"] = "disabled" };
                break;
            case ThinkingModes.Low:
                body["thinking"] = new JsonObject { ["type"] = "enabled" };
                body["reasoning_effort"] = "low";
                break;
            case ThinkingModes.High:
                body["thinking"] = new JsonObject { ["type"] = "enabled" };
                body["reasoning_effort"] = "high";
                break;
        }
    }

    public virtual string BuildUrl(string baseUrl)
    {
        var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (trimmed.Length == 0)
        {
            trimmed = DefaultBaseUrl;
        }

        if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        // 裸域名（https://host 或 https://host/）→ 补 /v1；已有路径则只补 /chat/completions
        var path = Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ? uri.AbsolutePath.Trim('/') : string.Empty;
        return path.Length == 0
            ? $"{trimmed}/v1/chat/completions"
            : $"{trimmed}/chat/completions";
    }

    protected static JsonArray BuildMessages(ChatRequest request)
    {
        var messages = new JsonArray();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt });
        }

        foreach (var message in request.Messages)
        {
            messages.Add(new JsonObject
            {
                ["role"] = message.Role switch
                {
                    ChatRole.Assistant => "assistant",
                    _ => "user",
                },
                ["content"] = message.Content,
            });
        }

        return messages;
    }

    public virtual ChatCompletion ParseResponse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var text = ReadText(root);
        var usage = ReadUsage(root);
        var finishReason = TryGetString(root, "choices", 0, "finish_reason");

        return new ChatCompletion
        {
            Text = text,
            Usage = usage,
            FinishReason = finishReason,
            Raw = json,
        };
    }

    /// <summary>取 choices[0].delta/message.content（推理模型的 reasoning_content 一律忽略）。</summary>
    protected static string ReadText(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var choice = choices[0];
        foreach (var field in new[] { "delta", "message" })
        {
            if (choice.TryGetProperty(field, out var node)
                && node.ValueKind == JsonValueKind.Object
                && node.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.String)
            {
                return content.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    protected static ChatUsage ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return ChatUsage.Empty;
        }

        return new ChatUsage(GetInt(usage, "prompt_tokens"), GetInt(usage, "completion_tokens"));
    }

    protected static int GetInt(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;

    protected static string? TryGetString(JsonElement root, string arrayName, int index, string field)
    {
        if (!root.TryGetProperty(arrayName, out var array) || array.ValueKind != JsonValueKind.Array || array.GetArrayLength() <= index)
        {
            return null;
        }

        var item = array[index];
        return item.ValueKind == JsonValueKind.Object && item.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    public virtual string? ParseStreamLine(string line, ChatStreamState state)
    {
        var payload = ExtractPayload(line);
        if (payload is null)
        {
            return null;
        }

        if (payload.Length == 0)
        {
            return null;
        }

        if (string.Equals(payload, "[DONE]", StringComparison.OrdinalIgnoreCase))
        {
            state.Done = true;
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            var usage = ReadUsage(root);
            if (!usage.IsEmpty)
            {
                state.Usage = usage;
            }

            var finishReason = TryGetString(root, "choices", 0, "finish_reason");
            if (!string.IsNullOrEmpty(finishReason))
            {
                state.FinishReason = finishReason;
            }

            var text = ReadText(root);
            if (text.Length == 0)
            {
                return null;
            }

            state.Text.Append(text);
            return text;
        }
        catch (JsonException)
        {
            // 流里偶尔混入心跳/非 JSON 行：忽略即可，不要让整次翻译失败
            return null;
        }
    }

    /// <summary>从一行里取出数据载荷：兼容 <c>data: {json}</c>（SSE）与裸 NDJSON，过滤空行/注释/event 行。</summary>
    protected static string? ExtractPayload(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith(':'))
        {
            return null;
        }

        if (trimmed.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[5..].Trim();
        }

        return trimmed;
    }

    public virtual string DescribeError(HttpStatusCode statusCode, string? responseBody)
    {
        var detail = TryReadErrorMessage(responseBody);
        var code = (int)statusCode;
        var hint = code switch
        {
            401 => "API Key 无效或未授权",
            403 => "无权访问该模型",
            404 => "地址或模型名不存在",
            429 => "触发限流或额度不足",
            >= 500 => "服务端错误",
            _ => "请求失败",
        };

        return detail is null ? $"HTTP {code}：{hint}" : $"HTTP {code}：{hint}（{detail}）";
    }

    /// <summary>取 <c>error.message</c> / <c>message</c> / <c>detail</c>，各家的错误结构不同（其它协议适配器复用）。</summary>
    internal static string? TryReadErrorMessage(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    return error.GetString();
                }

                if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                {
                    return message.GetString();
                }
            }

            foreach (var field in new[] { "message", "detail" })
            {
                if (root.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // 非 JSON 错误体（网关/代理返回 HTML 等）
        }

        var trimmed = responseBody.Trim();
        return trimmed.Length == 0 ? null : trimmed.Length > 300 ? trimmed[..300] : trimmed;
    }
}

/// <summary>
/// Azure OpenAI：与 OpenAI 同体，但认证头是 <c>api-key</c>，地址是
/// <c>{endpoint}/openai/deployments/{部署名}/chat/completions?api-version=…</c>。
/// </summary>
public sealed class AzureOpenAiChatProtocol : OpenAiChatProtocol
{
    public const string DefaultApiVersion = "2024-10-21";

    public override string Key => ChatProtocolFactory.AzureOpenAi;

    public override string DisplayNameKey => "TranslationPage_Protocol_AzureOpenAi";

    public override string DefaultBaseUrl => "https://{资源名}.openai.azure.com";

    public override string DefaultModel => "gpt-4o-mini";

    protected override void ApplyAuth(HttpRequestMessage message, ChatEndpoint endpoint)
        => message.Headers.TryAddWithoutValidation("api-key", endpoint.ApiKey);

    public override string BuildUrl(string baseUrl)
    {
        var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        if (trimmed.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return $"{trimmed}/openai/deployments/{{model}}/chat/completions";
    }

    public override HttpRequestMessage BuildRequest(ChatRequest request)
    {
        var message = base.BuildRequest(request);
        var apiVersion = string.IsNullOrWhiteSpace(request.Endpoint.ApiVersion) ? DefaultApiVersion : request.Endpoint.ApiVersion.Trim();
        var model = Uri.EscapeDataString(request.Endpoint.Model);
        var url = message.RequestUri!.ToString().Replace("{model}", model, StringComparison.Ordinal);
        message.RequestUri = new Uri($"{url}{(url.Contains('?') ? '&' : '?')}api-version={Uri.EscapeDataString(apiVersion)}");
        return message;
    }
}
