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

    /// <summary>截断到最大长度（尾部加 …）。界面摘要用；发给模型的请用 <see cref="TruncateForModel"/>。</summary>
    public static string Truncate(string text, int max)
    {
        var normalized = text ?? string.Empty;
        return normalized.Length <= max ? normalized : normalized[..max] + "…";
    }

    /// <summary>
    /// 发给模型/写进上下文的截断（阶段 134）：尾部带上**可诊断的元信息**，告诉模型"数据不完整、原有多长、
    /// 该怎么办"。只留一个 "…" 时模型会把半张表当成完整数据（P1 的上下文卫生问题）。
    /// <para>返回值长度 = <paramref name="max"/> + 标记长度（标记不计入 max，保证正文取满）。</para>
    /// </summary>
    public static string TruncateForModel(string? text, int max)
    {
        var normalized = text ?? string.Empty;
        if (max <= 0 || normalized.Length <= max)
        {
            return normalized;
        }

        var marker = $"…（内容已截断：原文 {normalized.Length:N0} 字符，仅保留前 {max:N0} 字符；如需要完整数据请缩小查询范围或分页）";
        return normalized[..max] + marker;
    }

    /// <summary>ISK 千分位格式化（与界面口径一致）。</summary>
    public static string FormatIsk(double value) => value.ToString("N0");
}
