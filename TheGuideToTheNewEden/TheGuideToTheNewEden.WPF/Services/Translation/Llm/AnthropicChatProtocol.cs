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

        // 工具声明：Anthropic 的 tools 用 input_schema 而不是 parameters
        if (request.Tools is { Count: > 0 })
        {
            var tools = new JsonArray();
            foreach (var tool in request.Tools)
            {
                tools.Add(new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["input_schema"] = tool.Parameters ?? new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() },
                });
            }

            body["tools"] = tools;
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

    /// <summary>
    /// 把归一化消息转成 Anthropic 的 messages。
    /// <para>
    /// **连续的同角色消息要合并**：Anthropic 要求 user/assistant 交替（连续同角色它会在服务端合并，
    /// 但自己合并更可控，也避免某些兼容网关更严格）。两种情形都会出现连续 user：
    /// 工具结果的 <c>tool_result</c> 块，以及紧随其后的用户文本——本方法把它们并进**同一条** user 消息。
    /// </para>
    /// </summary>
    private static JsonArray BuildMessages(ChatRequest request)
    {
        var messages = new JsonArray();
        JsonArray? pendingUserBlocks = null;   // 连续 user 侧内容（tool_result / text）先攒着，遇到 assistant 或收尾时落盘

        foreach (var message in request.Messages)
        {
            // 工具结果：并入挂起的 user 消息
            if (message.Role == ChatRole.Tool)
            {
                pendingUserBlocks ??= new JsonArray();
                pendingUserBlocks.Add(new JsonObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = message.ToolCallId ?? string.Empty,
                    ["content"] = message.Content ?? string.Empty,
                });
                continue;
            }

            // 用户文本：也并入挂起的 user 消息（与 tool_result 同一条，Anthropic 侧语义等价）
            if (message.Role == ChatRole.User)
            {
                pendingUserBlocks ??= new JsonArray();
                pendingUserBlocks.Add(new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = message.Content ?? string.Empty,
                });
                continue;
            }

            if (pendingUserBlocks is not null)
            {
                messages.Add(new JsonObject { ["role"] = "user", ["content"] = pendingUserBlocks });
                pendingUserBlocks = null;
            }

            // 模型要求调用工具的 assistant 消息：content 为 tool_use 块（必须原样回传，含 input）
            if (message.ToolCalls is { Count: > 0 })
            {
                var blocks = new JsonArray();
                foreach (var call in message.ToolCalls)
                {
                    blocks.Add(new JsonObject
                    {
                        ["type"] = "tool_use",
                        ["id"] = call.Id,
                        ["name"] = call.Name,
                        ["input"] = TryParseJson(call.ArgumentsJson),
                    });
                }

                messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = blocks });
                continue;
            }

            messages.Add(new JsonObject
            {
                ["role"] = "assistant",
                ["content"] = new JsonArray
                {
                    new JsonObject { ["type"] = "text", ["text"] = message.Content ?? string.Empty },
                },
            });
        }

        if (pendingUserBlocks is not null)
        {
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = pendingUserBlocks });
        }

        return messages;
    }

    /// <summary>把工具参数/结果 JSON 串解析成 JsonNode（失败时兜底成对象，避免整条消息作废）。</summary>
    private static JsonNode TryParseJson(string json)
    {
        try
        {
            return JsonNode.Parse(json) ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject { ["value"] = json };
        }
    }

    public ChatCompletion ParseResponse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var text = ReadText(root);
        var usage = ReadUsage(root);
        var finishReason = root.TryGetProperty("stop_reason", out var stop) && stop.ValueKind == JsonValueKind.String ? stop.GetString() : null;

        return new ChatCompletion
        {
            Text = text,
            Usage = usage,
            FinishReason = finishReason,
            ToolCalls = ParseToolCalls(json),
            Raw = json,
        };
    }

    public IReadOnlyList<ChatToolCall>? ParseToolCalls(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var result = new List<ChatToolCall>();
            foreach (var block in content.EnumerateArray())
            {
                if (block.ValueKind != JsonValueKind.Object
                    || !block.TryGetProperty("type", out var type)
                    || type.ValueKind != JsonValueKind.String
                    || type.GetString() != "tool_use"
                    || !block.TryGetProperty("id", out var id)
                    || id.ValueKind != JsonValueKind.String
                    || !block.TryGetProperty("name", out var name)
                    || name.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var args = block.TryGetProperty("input", out var input) && input.ValueKind != JsonValueKind.Null
                    ? input.GetRawText()
                    : "{}";
                result.Add(new ChatToolCall(id.GetString() ?? string.Empty, name.GetString() ?? string.Empty, args));
            }

            return result.Count == 0 ? null : result;
        }
        catch (JsonException)
        {
            return null;
        }
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

        // 阶段 151：Anthropic 的前缀缓存命中量在 cache_read_input_tokens（写入量在 cache_creation_input_tokens，
        // 不计入"命中"）。只有开启了 cache_control 的请求才会返回非零。
        return new ChatUsage(GetInt(usage, "input_tokens"), GetInt(usage, "output_tokens"))
        {
            PromptCacheHitTokens = GetInt(usage, "cache_read_input_tokens"),
        };
    }

    private static int GetInt(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;

    /// <summary>取事件里的 content block <c>index</c>（累积器按键）。</summary>
    private static int ReadIndex(JsonElement root)
        => root.TryGetProperty("index", out var node) && node.ValueKind == JsonValueKind.Number ? node.GetInt32() : 0;

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
                case "content_block_start":
                    if (root.TryGetProperty("content_block", out var block)
                        && block.ValueKind == JsonValueKind.Object
                        && block.TryGetProperty("type", out var blockType)
                        && blockType.ValueKind == JsonValueKind.String
                        && blockType.GetString() == "tool_use")
                    {
                        var startIndex = ReadIndex(root);
                        var acc = state.EnsureToolCall(startIndex);
                        if (block.TryGetProperty("id", out var idNode) && idNode.ValueKind == JsonValueKind.String)
                        {
                            acc.Id = idNode.GetString() ?? string.Empty;
                        }

                        if (block.TryGetProperty("name", out var nameNode) && nameNode.ValueKind == JsonValueKind.String)
                        {
                            acc.Name = nameNode.GetString() ?? string.Empty;
                        }

                        // 流式下 content_block_start 的 input 是**空对象**（真正的 JSON 走 input_json_delta）——
                        // 只有非空 input（极少见：服务端一次性给全参数）才追加，否则会拼出 {}{"itemName":…} 这种非法 JSON
                        if (block.TryGetProperty("input", out var inputNode)
                            && inputNode.ValueKind == JsonValueKind.Object
                            && inputNode.EnumerateObject().Any())
                        {
                            acc.Arguments.Append(inputNode.GetRawText());
                        }
                    }

                    return null;

                case "content_block_delta":
                    if (root.TryGetProperty("delta", out var delta)
                        && delta.ValueKind == JsonValueKind.Object)
                    {
                        // 工具参数的流式增量：partial_json 分片累积
                        if (delta.TryGetProperty("type", out var deltaType)
                            && deltaType.ValueKind == JsonValueKind.String
                            && deltaType.GetString() == "input_json_delta"
                            && delta.TryGetProperty("partial_json", out var partial)
                            && partial.ValueKind == JsonValueKind.String)
                        {
                            var deltaIndex = ReadIndex(root);
                            state.EnsureToolCall(deltaIndex).Arguments.Append(partial.GetString());
                            return null;
                        }

                        if (delta.TryGetProperty("text", out var text)
                            && text.ValueKind == JsonValueKind.String)
                        {
                            var piece = text.GetString() ?? string.Empty;
                            state.Text.Append(piece);
                            return piece;
                        }
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
                        state.Usage = new ChatUsage(GetInt(startUsage, "input_tokens"), state.Usage.OutputTokens)
                        {
                            PromptCacheHitTokens = GetInt(startUsage, "cache_read_input_tokens"),
                        };
                    }

                    return null;

                case "message_stop":
                    state.Done = true;
                    state.FinalizeToolCalls();
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
