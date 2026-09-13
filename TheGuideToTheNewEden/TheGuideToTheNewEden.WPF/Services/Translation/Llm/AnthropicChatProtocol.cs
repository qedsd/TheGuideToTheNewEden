using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TheGuideToTheNewEden.WPF.Services.Translation.Llm;

/// <summary>
/// Anthropic Messages 协议（<c>POST {base}/v1/messages</c>）。
/// 与 OpenAI 的三处差异：认证头是 <c>x-api-key</c> + 必带的 <c>anthropic-version</c>；
/// system 是<b>顶层字段</b>而不是一条 message；<c>max_tokens</c> 必填。
/// </summary>
public sealed class AnthropicChatProtocol : IChatProtocol
{
    /// <summary>Anthropic 要求的 API 版本头。</summary>
    public const string ApiVersion = "2023-06-01";

    /// <summary>max_tokens 必填，用户没填时用这个值。</summary>
    public const int FallbackMaxTokens = 1024;

    public string Key => ChatProtocolFactory.Anthropic;

    public string DisplayNameKey => "TranslationPage_Protocol_Anthropic";

    public string DefaultBaseUrl => "https://api.anthropic.com";

    public string DefaultModel => "claude-3-5-haiku-latest";

    public HttpRequestMessage BuildRequest(ChatRequest request)
    {
        var endpoint = request.Endpoint;
        var body = new JsonObject
        {
            ["model"] = endpoint.Model,
            ["max_tokens"] = endpoint.MaxTokens > 0 ? endpoint.MaxTokens : FallbackMaxTokens,
            ["messages"] = BuildMessages(request),
            ["stream"] = request.Stream,
        };

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            body["system"] = request.SystemPrompt;
        }

        if (endpoint.Temperature > 0)
        {
            body["temperature"] = endpoint.Temperature;
        }

        var message = new HttpRequestMessage(HttpMethod.Post, BuildUrl(endpoint.BaseUrl, request.Stream))
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        message.Headers.TryAddWithoutValidation("x-api-key", endpoint.ApiKey);
        message.Headers.TryAddWithoutValidation("anthropic-version", ApiVersion);
        return message;
    }

    public string BuildUrl(string baseUrl, bool stream = false)
    {
        var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (trimmed.Length == 0)
        {
            trimmed = DefaultBaseUrl;
        }

        if (trimmed.EndsWith("/messages", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var path = Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ? uri.AbsolutePath.Trim('/') : string.Empty;
        return path.EndsWith("v1", StringComparison.OrdinalIgnoreCase)
            ? $"{trimmed}/messages"
            : $"{trimmed}/v1/messages";
    }

    private static JsonArray BuildMessages(ChatRequest request)
    {
        var messages = new JsonArray();
        foreach (var message in request.Messages)
        {
            messages.Add(new JsonObject
            {
                ["role"] = message.Role == ChatRole.Assistant ? "assistant" : "user",
                ["content"] = new JsonArray
                {
                    new JsonObject { ["type"] = "text", ["text"] = message.Content },
                },
            });
        }

        return messages;
    }

    public ChatCompletion ParseResponse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var text = ReadText(root);
        var usage = ReadUsage(root);
        var finishReason = root.TryGetProperty("stop_reason", out var stop) && stop.ValueKind == JsonValueKind.String ? stop.GetString() : null;

        return new ChatCompletion { Text = text, Usage = usage, FinishReason = finishReason, Raw = json };
    }

    /// <summary>拼接 content 数组里的所有 text 块。</summary>
    private static string ReadText(JsonElement root)
    {
        if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind == JsonValueKind.Object
                && block.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && type.GetString() == "text"
                && block.TryGetProperty("text", out var text)
                && text.ValueKind == JsonValueKind.String)
            {
                builder.Append(text.GetString());
            }
        }

        return builder.ToString();
    }

    private static ChatUsage ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return ChatUsage.Empty;
        }

        return new ChatUsage(GetInt(usage, "input_tokens"), GetInt(usage, "output_tokens"));
    }

    private static int GetInt(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;

    public string? ParseStreamLine(string line, ChatStreamState state)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith(':') || trimmed.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var payload = trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? trimmed[5..].Trim() : trimmed;
        if (payload.Length == 0 || payload == "[DONE]")
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeNode) && typeNode.ValueKind == JsonValueKind.String ? typeNode.GetString() : null;

            switch (type)
            {
                case "content_block_delta":
                    if (root.TryGetProperty("delta", out var delta)
                        && delta.ValueKind == JsonValueKind.Object
                        && delta.TryGetProperty("text", out var text)
                        && text.ValueKind == JsonValueKind.String)
                    {
                        var piece = text.GetString() ?? string.Empty;
                        state.Text.Append(piece);
                        return piece;
                    }

                    return null;

                case "message_delta":
                    if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
                    {
                        var output = GetInt(usage, "output_tokens");
                        state.Usage = state.Usage with { OutputTokens = output };
                    }

                    if (root.TryGetProperty("delta", out var messageDelta)
                        && messageDelta.ValueKind == JsonValueKind.Object
                        && messageDelta.TryGetProperty("stop_reason", out var stopReason)
                        && stopReason.ValueKind == JsonValueKind.String)
                    {
                        state.FinishReason = stopReason.GetString();
                    }

                    return null;

                case "message_start":
                    if (root.TryGetProperty("message", out var message)
                        && message.ValueKind == JsonValueKind.Object
                        && message.TryGetProperty("usage", out var startUsage)
                        && startUsage.ValueKind == JsonValueKind.Object)
                    {
                        state.Usage = new ChatUsage(GetInt(startUsage, "input_tokens"), state.Usage.OutputTokens);
                    }

                    return null;

                case "message_stop":
                    state.Done = true;
                    return null;

                case "error":
                    var errorMessage = root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object
                        ? error.TryGetProperty("message", out var message2) && message2.ValueKind == JsonValueKind.String ? message2.GetString() : null
                        : null;
                    throw new ChatException(errorMessage ?? "流式响应返回错误");

                default:
                    return null;
            }
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string DescribeError(HttpStatusCode statusCode, string? responseBody)
    {
        var detail = OpenAiChatProtocol.TryReadErrorMessage(responseBody);
        var code = (int)statusCode;
        var hint = code switch
        {
            401 => "API Key 无效",
            403 => "无权访问该模型",
            404 => "地址或模型名不存在",
            429 => "触发限流或额度不足",
            >= 500 => "服务端错误",
            _ => "请求失败",
        };

        return detail is null ? $"HTTP {code}：{hint}" : $"HTTP {code}：{hint}（{detail}）";
    }
}
