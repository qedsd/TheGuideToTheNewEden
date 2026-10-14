using System.Text.Json;

namespace TheGuideToTheNewEden.WPF.Services.Map;

/// <summary>
/// 跳桥清单解析（AI 助手 <c>map_jumpbridge_import</c> 用）：
/// 支持三种常见格式——每行一组 <c>A ↔ B</c>（兼容 <c>-</c>、<c>→</c>、<c>,</c> 分隔），
/// JSON 数组 <c>[{"System1": "...", "System2": "..."}]</c>（值可为星系名或 ID 数字），CSV 两列。
/// </summary>
public static class JumpBridgeTextParser
{
    /// <summary>一组待解析的跳桥（两端为原始文本；是否星系名/ID 由调用方解析）。</summary>
    public sealed record ParsedBridge(string? SystemA, string? SystemB);

    public static List<ParsedBridge> Parse(string? text)
    {
        var result = new List<ParsedBridge>();
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return result;
        }

        if (trimmed.StartsWith('['))
        {
            ParseJson(trimmed, result);
            return result;
        }

        foreach (var rawLine in trimmed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var line = rawLine.Replace('↔', '|').Replace('→', '|').Replace('←', '|').Replace('-', '|').Replace(',', '|');
            var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2)
            {
                result.Add(new ParsedBridge(parts[0], parts[1]));
            }
        }

        return result;
    }

    private static void ParseJson(string json, List<ParsedBridge> result)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                var a = ReadField(item, "System1");
                var b = ReadField(item, "System2");
                if (a is not null && b is not null)
                {
                    result.Add(new ParsedBridge(a, b));
                }
            }
        }
        catch (JsonException)
        {
            // 非 JSON（可能只是以 [ 开头的文本）：交给逐行分支已无意义，忽略
        }
    }

    private static string? ReadField(JsonElement item, string name)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetInt32().ToString(),
            _ => null,
        };
    }
}
