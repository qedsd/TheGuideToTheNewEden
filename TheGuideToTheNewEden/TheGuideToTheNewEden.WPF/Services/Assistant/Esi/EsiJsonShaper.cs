using System.Text;
using System.Text.Json;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Esi;

/// <summary>
/// ESI 响应的整形器：把原始 JSON 压成"模型读得动、token 花得起"的紧凑文本。
/// <para>
/// 为什么不直接回原始 JSON：① 结算给模型的单条工具结果有 4000 字符上限（<c>AiAssistantEngine.MaxToolResultChars</c>），
/// 原始 JSON 会被从中间切断、留下半截对象；② 一个物品的订单页动辄上千条，
/// 全量回填既烧 token 又淹没重点；③ 界面本身渲染 Markdown（阶段 109），
/// **数组对象转成表格**正好是最省 token 又好读的形式。
/// </para>
/// 规则：对象 → <c>键: 值</c> 列表；对象数组 → Markdown 表格（最多 8 列 / 30 行）；
/// 标量数组 → 逗号连接（最多 60 项）；长字符串单独截断；最后整体按 <c>maxChars</c> 硬截断并注明。
/// </summary>
public static class EsiJsonShaper
{
    public const int DefaultMaxChars = 3500;

    private const int MaxColumns = 8;
    private const int MaxRows = 30;
    private const int MaxCellChars = 40;
    private const int MaxKeys = 40;
    private const int MaxScalarItems = 60;
    private const int MaxInlineChars = 80;

    /// <summary>整形；<paramref name="json"/> 不是合法 JSON 时按纯文本处理（同样截断）。</summary>
    public static string Shape(string? json, int maxChars = DefaultMaxChars)
    {
        var limit = Math.Max(200, maxChars);
        var text = json ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return "（空响应）";
        }

        string shaped;
        JsonDocument? document = null;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            document = null;
        }

        if (document is null)
        {
            shaped = text.Trim();
            return Clamp(shaped, limit);
        }

        using (document)
        {
            var builder = new StringBuilder();
            AppendElement(builder, document.RootElement, 0);
            shaped = builder.ToString().TrimEnd();
        }

        return Clamp(shaped, limit);
    }

    /// <summary>数组元素个数（给调用方在头行里写"共 N 条"）。</summary>
    public static int CountItems(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return 0;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.Array => document.RootElement.GetArrayLength(),
                JsonValueKind.Object => document.RootElement.EnumerateObject().Count(),
                _ => 1,
            };
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static void AppendElement(StringBuilder builder, JsonElement element, int indent)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                AppendArray(builder, element, indent);
                break;
            case JsonValueKind.Object:
                AppendObject(builder, element, indent);
                break;
            default:
                builder.Append(Scalar(element));
                break;
        }
    }

    private static void AppendArray(StringBuilder builder, JsonElement array, int indent)
    {
        var length = array.GetArrayLength();
        if (length == 0)
        {
            builder.Append("（空列表）");
            return;
        }

        var first = array.EnumerateArray().First();
        if (first.ValueKind == JsonValueKind.Object)
        {
            AppendTable(builder, array, indent, length);
            return;
        }

        if (first.ValueKind is JsonValueKind.Array)
        {
            // 嵌套数组（少见于 ESI）：逐行紧凑输出
            var index = 0;
            foreach (var item in array.EnumerateArray())
            {
                if (index++ >= MaxRows)
                {
                    builder.Append(Indent(indent)).Append($"…（另有 {length - MaxRows} 项）");
                    return;
                }

                builder.Append(Indent(indent));
                AppendElement(builder, item, indent + 1);
                builder.AppendLine();
            }

            return;
        }

        var scalars = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (scalars.Count >= MaxScalarItems)
            {
                break;
            }

            scalars.Add(Scalar(item));
        }

        builder.Append(string.Join(", ", scalars));
        if (length > scalars.Count)
        {
            builder.Append($"（共 {length} 项）");
        }
    }

    private static void AppendTable(StringBuilder builder, JsonElement array, int indent, int length)
    {
        var columns = new List<string>();
        var sample = 0;
        foreach (var item in array.EnumerateArray())
        {
            foreach (var property in item.EnumerateObject())
            {
                if (!columns.Contains(property.Name) && columns.Count < MaxColumns)
                {
                    columns.Add(property.Name);
                }
            }

            if (++sample >= 3 || columns.Count >= MaxColumns)
            {
                break;
            }
        }

        if (columns.Count == 0)
        {
            builder.Append($"（{length} 条空对象）");
            return;
        }

        var prefix = Indent(indent);
        builder.Append(prefix).Append("| ").Append(string.Join(" | ", columns)).AppendLine(" |");
        builder.Append(prefix).Append('|').Append(string.Join("|", columns.Select(_ => "---"))).AppendLine("|");

        var rows = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (rows++ >= MaxRows)
            {
                builder.Append(prefix).Append($"…（另有 {length - MaxRows} 条）");
                return;
            }

            builder.Append(prefix).Append("| ");
            for (var i = 0; i < columns.Count; i++)
            {
                var cell = item.TryGetProperty(columns[i], out var value) ? Cell(value) : "-";
                builder.Append(cell);
                builder.Append(i == columns.Count - 1 ? " |" : " | ");
            }

            builder.AppendLine();
        }
    }

    private static void AppendObject(StringBuilder builder, JsonElement element, int indent)
    {
        var count = element.EnumerateObject().Count();
        if (count == 0)
        {
            builder.Append("（空对象）");
            return;
        }

        var index = 0;
        foreach (var property in element.EnumerateObject())
        {
            if (index++ >= MaxKeys)
            {
                builder.Append(Indent(indent)).Append($"…（另有 {count - MaxKeys} 个字段）");
                return;
            }

            builder.Append(Indent(indent)).Append("- ").Append(property.Name).Append(": ");
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                if (indent >= 2)
                {
                    builder.Append(Compact(property.Value));
                }
                else
                {
                    builder.AppendLine();
                    AppendElement(builder, property.Value, indent + 1);
                }
            }
            else
            {
                builder.Append(Scalar(property.Value));
            }

            builder.AppendLine();
        }
    }

    private static string Cell(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => Clean(element.GetString(), MaxCellChars),
        JsonValueKind.Null or JsonValueKind.Undefined => "-",
        JsonValueKind.Object or JsonValueKind.Array => Clean(Compact(element), MaxCellChars),
        _ => element.ToString(),
    };

    private static string Scalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => Clean(element.GetString(), MaxInlineChars),
        JsonValueKind.Null or JsonValueKind.Undefined => "null",
        JsonValueKind.Object or JsonValueKind.Array => Clean(Compact(element), MaxInlineChars),
        _ => element.ToString(),
    };

    /// <summary>嵌套过深时用的紧凑写法（去掉换行，减少行数）。</summary>
    private static string Compact(JsonElement element)
    {
        var raw = element.GetRawText();
        return raw.Length <= MaxInlineChars ? raw : raw[..MaxInlineChars] + "…";
    }

    /// <summary>单元格/单值清理：去掉换行与表格分隔符，避免把表格结构弄坏。</summary>
    private static string Clean(string? text, int limit)
    {
        var value = (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("|", "\\|").Trim();
        return value.Length <= limit ? value : value[..(limit - 1)] + "…";
    }

    private static string Clamp(string text, int limit)
        => text.Length <= limit ? text : text[..(limit - 1)] + "…（内容过长已截断）";

    private static string Indent(int indent) => new(' ', indent * 2);
}
