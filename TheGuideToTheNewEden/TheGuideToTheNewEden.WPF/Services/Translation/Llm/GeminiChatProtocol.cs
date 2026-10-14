using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TheGuideToTheNewEden.WPF.Services.Translation.Llm;

/// <summary>
/// Google Gemini 协议（<c>POST {base}/v1beta/models/{model}:generateContent</c>，流式加 <c>?alt=sse</c>）。
/// 差异：认证头 <c>x-goog-api-key</c>；system 用顶层 <c>systemInstruction</c>；
/// 消息体是 <c>contents[{role,parts[{text}]}]</c>；角色用 <c>user</c>/<c>model</c>；
/// 输出 token 上限字段是 <c>generationConfig.maxOutputTokens</c>。
/// </summary>
public sealed class GeminiChatProtocol : IChatProtocol
{
    public string Key => ChatProtocolFactory.Gemini;

    public string DisplayNameKey => "TranslationPage_Protocol_Gemini";

    public string DefaultBaseUrl => "https://generativelanguage.googleapis.com";

    public string DefaultModel => "gemini-2.0-flash";

    public HttpRequestMessage BuildRequest(ChatRequest request)
    {
        var endpoint = request.Endpoint;
        var generationConfig = new JsonObject();
        if (endpoint.Temperature > 0)
        {
            generationConfig["temperature"] = endpoint.Temperature;
        }

        if (endpoint.MaxTokens > 0)
        {
            generationConfig["maxOutputTokens"] = endpoint.MaxTokens;
        }

        if (request.JsonMode)
        {
            generationConfig["responseMimeType"] = "application/json";
        }

        var body = new JsonObject
        {
            ["contents"] = BuildContents(request),
        };

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            body["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray { new JsonObject { ["text"] = request.SystemPrompt } },
            };
        }

        // 工具声明：Gemini 用 tools[].functionDeclarations[]
        if (request.Tools is { Count: > 0 })
        {
            var declarations = new JsonArray();
            foreach (var tool in request.Tools)
            {
                declarations.Add(new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = tool.Parameters ?? new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() },
                });
            }

            body["tools"] = new JsonArray { new JsonObject { ["functionDeclarations"] = declarations } };
        }

        if (generationConfig.Count > 0)
        {
            body["generationConfig"] = generationConfig;
        }

        var message = new HttpRequestMessage(HttpMethod.Post, BuildUrl(endpoint.BaseUrl, endpoint.Model, request.Stream))
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        message.Headers.TryAddWithoutValidation("x-goog-api-key", endpoint.ApiKey);
        return message;
    }

    public string BuildUrl(string baseUrl, string model, bool stream)
    {
        var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (trimmed.Length == 0)
        {
            trimmed = DefaultBaseUrl;
        }

        var method = stream ? "streamGenerateContent" : "generateContent";
        if (trimmed.Contains(":generateContent", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains(":streamGenerateContent", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = stream && !trimmed.Contains("alt=sse", StringComparison.OrdinalIgnoreCase)
                ? $"{trimmed}{(trimmed.Contains('?') ? '&' : '?')}alt=sse"
                : trimmed;
            return suffix;
        }

        var escapedModel = Uri.EscapeDataString(model);
        var path = Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ? uri.AbsolutePath.Trim('/') : string.Empty;
        var url = path.EndsWith("v1beta", StringComparison.OrdinalIgnoreCase) || path.EndsWith("v1", StringComparison.OrdinalIgnoreCase)
            ? $"{trimmed}/models/{escapedModel}:{method}"
            : $"{trimmed}/v1beta/models/{escapedModel}:{method}";

        return stream ? $"{url}?alt=sse" : url;
    }

    private static JsonArray BuildContents(ChatRequest request)
    {
        var contents = new JsonArray();
        JsonObject? pendingFunctionUser = null;   // 连续 Tool 消息合并为一条 user

        foreach (var message in request.Messages)
        {
            // 工具结果：functionResponse 并入挂起的 user 消息（name 取自 Tool 消息自带的 ToolName，
            // 与 assistant(functionCall) 里的名字一致——不依赖顺序配对，确认挂起导致的乱序回填也安全）
            if (message.Role == ChatRole.Tool)
            {
                pendingFunctionUser ??= new JsonObject { ["role"] = "user", ["parts"] = new JsonArray() };
                ((JsonArray)pendingFunctionUser["parts"]!).Add(new JsonObject
                {
                    ["functionResponse"] = new JsonObject
                    {
                        ["name"] = message.ToolName ?? string.Empty,
                        ["response"] = new JsonObject { ["result"] = message.Content ?? string.Empty },
                    },
                });
                continue;
            }

            if (pendingFunctionUser is not null)
            {
                contents.Add(pendingFunctionUser);
                pendingFunctionUser = null;
            }

            // 模型要求调用工具的 assistant 消息：role=model + functionCall parts
            if (message.Role == ChatRole.Assistant && message.ToolCalls is { Count: > 0 })
            {
                var parts = new JsonArray();
                foreach (var call in message.ToolCalls)
                {
                    parts.Add(new JsonObject
                    {
                        ["functionCall"] = new JsonObject
                        {
                            ["name"] = call.Name,
                            ["args"] = TryParseJson(call.ArgumentsJson),
                        },
                    });
                }

                contents.Add(new JsonObject { ["role"] = "model", ["parts"] = parts });
                continue;
            }

            contents.Add(new JsonObject
            {
                ["role"] = message.Role == ChatRole.Assistant ? "model" : "user",
                ["parts"] = new JsonArray { new JsonObject { ["text"] = message.Content ?? string.Empty } },
            });
        }

        if (pendingFunctionUser is not null)
        {
            contents.Add(pendingFunctionUser);
        }

        return contents;
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

        return new ChatCompletion
        {
            Text = ReadText(root),
            Usage = ReadUsage(root),
            FinishReason = ReadFinishReason(root),
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
            if (!root.TryGetProperty("candidates", out var candidates)
                || candidates.ValueKind != JsonValueKind.Array
                || candidates.GetArrayLength() == 0
                || !candidates[0].TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var result = new List<ChatToolCall>();
            var index = 0;
            foreach (var part in parts.EnumerateArray())
            {
                if (part.ValueKind != JsonValueKind.Object
                    || !part.TryGetProperty("functionCall", out var call)
                    || call.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = call.TryGetProperty("name", out var nameNode) && nameNode.ValueKind == JsonValueKind.String
                    ? nameNode.GetString()
                    : string.Empty;
                var args = call.TryGetProperty("args", out var argsNode) && argsNode.ValueKind != JsonValueKind.Null
                    ? argsNode.GetRawText()
                    : "{}";
                // Gemini 没有 tool call id：生成 fc-N，供引擎记账；回传按 name/顺序匹配
                result.Add(new ChatToolCall($"fc-{index++}", name ?? string.Empty, args));
            }

            return result.Count == 0 ? null : result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ReadText(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        if (!candidates[0].TryGetProperty("content", out var content)
            || !content.TryGetProperty("parts", out var parts)
            || parts.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.Object && part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            {
                builder.Append(text.GetString());
            }
        }

        return builder.ToString();
    }

    private static ChatUsage ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usageMetadata", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return ChatUsage.Empty;
        }

        return new ChatUsage(GetInt(usage, "promptTokenCount"), GetInt(usage, "candidatesTokenCount"));
    }

    private static string? ReadFinishReason(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
        {
            return null;
        }

        return candidates[0].TryGetProperty("finishReason", out var reason) && reason.ValueKind == JsonValueKind.String
            ? reason.GetString()
            : null;
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

            var usage = ReadUsage(root);
            if (!usage.IsEmpty)
            {
                state.Usage = usage;
            }

            var finishReason = ReadFinishReason(root);
            if (!string.IsNullOrEmpty(finishReason))
            {
                state.FinishReason = finishReason;
                state.FinalizeToolCalls();
            }

            // 工具调用：parts 里的 functionCall（每 chunk 重复全量 parts，按名字去重累积）
            TryReadFunctionCall(root, state);

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
            return null;
        }
    }

    /// <summary>读取 parts 里的 functionCall 并累积进 <paramref name="state"/>（按名字去重，幂等）。</summary>
    private static void TryReadFunctionCall(JsonElement root, ChatStreamState state)
    {
        if (!root.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array
            || candidates.GetArrayLength() == 0
            || !candidates[0].TryGetProperty("content", out var content)
            || !content.TryGetProperty("parts", out var parts)
            || parts.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var part in parts.EnumerateArray())
        {
            if (part.ValueKind != JsonValueKind.Object
                || !part.TryGetProperty("functionCall", out var call)
                || call.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = call.TryGetProperty("name", out var nameNode) && nameNode.ValueKind == JsonValueKind.String
                ? nameNode.GetString()
                : null;
            if (string.IsNullOrEmpty(name) || state.SeenFunctionCalls.Contains(name))
            {
                continue;
            }

            state.SeenFunctionCalls.Add(name);
            var acc = state.EnsureToolCall(state.FunctionCallCount++);
            acc.Name = name;
            if (call.TryGetProperty("args", out var argsNode) && argsNode.ValueKind != JsonValueKind.Null)
            {
                acc.Arguments.Append(argsNode.GetRawText());
            }
        }
    }

    public string DescribeError(HttpStatusCode statusCode, string? responseBody)
    {
        var detail = OpenAiChatProtocol.TryReadErrorMessage(responseBody);
        var code = (int)statusCode;
        var hint = code switch
        {
            400 => "请求被拒绝（模型名/参数或地区限制）",
            401 or 403 => "API Key 无效或无权访问",
            404 => "模型不存在",
            429 => "触发限流或额度不足",
            >= 500 => "服务端错误",
            _ => "请求失败",
        };

        return detail is null ? $"HTTP {code}：{hint}" : $"HTTP {code}：{hint}（{detail}）";
    }
}
