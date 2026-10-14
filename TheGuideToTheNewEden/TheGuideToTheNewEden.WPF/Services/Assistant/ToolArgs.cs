using System.Text.Json;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>工具实现共用的小工具：参数读取、截断、ISK 格式化。</summary>
public static class ToolArgs
{
    /// <summary>读字符串参数（缺省/非字符串返回 null）。</summary>
    public static string? ReadString(JsonElement args, string name)
    {
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    /// <summary>读整数参数（缺省/非法用 fallback）。</summary>
    public static int ReadInt(JsonElement args, string name, int fallback)
    {
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number)
        {
            return value.GetInt32();
        }

        return fallback;
    }

    /// <summary>读布尔参数（缺省/非法用 fallback）。</summary>
    public static bool ReadBool(JsonElement args, string name, bool fallback)
    {
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty(name, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        return fallback;
    }

    /// <summary>截断到最大长度（尾部加 …）。</summary>
    public static string Truncate(string text, int max)
    {
        var normalized = text ?? string.Empty;
        return normalized.Length <= max ? normalized : normalized[..max] + "…";
    }

    /// <summary>ISK 千分位格式化（与界面口径一致）。</summary>
    public static string FormatIsk(double value) => value.ToString("N0");
}
