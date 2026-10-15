using System.Text.Json;

namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// 请求体断言的辅助：<c>System.Text.Json</c> 默认会把非 ASCII 转义成 <c>\uXXXX</c>，
/// 所以**不能**对原始 JSON 串做 <c>Contains("中文")</c>——必须解析后按解码文本搜索。
/// </summary>
internal static class ProbeJson
{
    /// <summary>请求体（解析后）的任意字符串值里是否包含 <paramref name="needle"/>。</summary>
    public static bool Contains(string json, string needle)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return Walk(document.RootElement, needle);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>请求体里是否出现某个键名（如 <c>tools</c> / <c>tool_choice</c>）。</summary>
    public static bool HasProperty(string json, string name)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return HasProperty(document.RootElement, name);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>取顶层 <c>messages</c> / <c>contents</c> 数组里第 <paramref name="index"/> 条的角色（取不到返回空串）。</summary>
    public static string RoleAt(string json, string arrayName, int index)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(arrayName, out var array)
                || array.ValueKind != JsonValueKind.Array
                || index >= array.GetArrayLength())
            {
                return string.Empty;
            }

            return array[index].TryGetProperty("role", out var role) && role.ValueKind == JsonValueKind.String
                ? role.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    /// <summary>取顶层 <c>messages</c> / <c>contents</c> 数组长度。</summary>
    public static int ArrayLength(string json, string arrayName)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty(arrayName, out var array) && array.ValueKind == JsonValueKind.Array
                ? array.GetArrayLength()
                : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    /// <summary>按文档顺序把所有字符串值拼成一段文本（用于断言"谁在谁前面"，如上下文问答的顺序）。</summary>
    public static string Flatten(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var builder = new System.Text.StringBuilder();
            Flatten(document.RootElement, builder);
            return builder.ToString();
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static void Flatten(JsonElement element, System.Text.StringBuilder builder)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                builder.Append(element.GetString()).Append('\n');
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Flatten(property.Value, builder);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Flatten(item, builder);
                }

                break;
        }
    }

    private static bool Walk(JsonElement element, string needle)
        => element.ValueKind switch
        {
            JsonValueKind.String => (element.GetString() ?? string.Empty).Contains(needle, StringComparison.Ordinal),
            JsonValueKind.Object => element.EnumerateObject().Any(p => Walk(p.Value, needle)),
            JsonValueKind.Array => element.EnumerateArray().Any(item => Walk(item, needle)),
            _ => false,
        };

    private static bool HasProperty(JsonElement element, string name)
        => element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .Any(p => p.NameEquals(name) || HasProperty(p.Value, name)),
            JsonValueKind.Array => element.EnumerateArray().Any(item => HasProperty(item, name)),
            _ => false,
        };
}
