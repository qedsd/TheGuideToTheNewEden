using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Map;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 从文本/文件批量导入跳桥（写操作，需用户确认后执行）。
/// 支持 每行「A ↔ B」/「A - B」/「A,B」、JSON 数组、CSV 两列（见 <see cref="JumpBridgeTextParser"/>）。
/// 确认后：只添加「两端都空闲」的组；冲突（任一端已占用）与未识别的组跳过并在结果里说明。
/// </summary>
public sealed class MapJumpBridgeImportTool : ITool
{
    public string Name => "map_jumpbridge_import";

    public string DisplayNameKey => "AssistantTool_map_jumpbridge_import";

    public string Description =>
        "从一段文本或本地文件批量导入玩家跳桥清单。text 与 filePath 二选一：text 直接粘贴（每行「A ↔ B」或「A - B」或「A,B」，" +
        "或 JSON 数组 [{System1, System2}]）；filePath 给本机文件路径（工具读取）。该操作会批量写入跳桥配置，" +
        "需要用户确认后才会执行；已被占用的星系自动跳过并在结果里列出。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["text"] = new JsonObject { ["type"] = "string", ["description"] = "跳桥清单文本（与 filePath 二选一）" },
            ["filePath"] = new JsonObject { ["type"] = "string", ["description"] = "本机文件路径（与 text 二选一）" },
        },
    };

    public bool RequiresConfirmation => true;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var text = ToolArgs.ReadString(args, "text");
        var filePath = ToolArgs.ReadString(args, "filePath");

        string content;
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            try
            {
                content = File.ReadAllText(filePath.Trim());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return Task.FromResult(new ToolResult($"读取文件失败：{ex.Message}"));
            }
        }
        else
        {
            content = text ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return Task.FromResult(new ToolResult("没有可导入的内容（text 或 filePath 至少给一个）。"));
        }

        var bridges = JumpBridgeTextParser.Parse(content);
        if (bridges.Count == 0)
        {
            return Task.FromResult(new ToolResult("没有解析出任何跳桥，请用「A ↔ B」每行一组或 JSON 数组格式。"));
        }

        var added = 0;
        var conflicts = new List<string>();
        var skipped = new List<string>();
        foreach (var bridge in bridges)
        {
            var a = ResolveEnd(bridge.SystemA);
            var b = ResolveEnd(bridge.SystemB);
            if (a is null || b is null)
            {
                skipped.Add($"{bridge.SystemA ?? "?"} ↔ {bridge.SystemB ?? "?"}（未识别）");
                continue;
            }

            var addedOk = JumpBridgeSettingService.Add(a.Value, b.Value);
            if (addedOk)
            {
                added++;
            }
            else
            {
                var aName = ToolNameResolver.ResolveSystemById(a.Value) ?? a.Value.ToString();
                var bName = ToolNameResolver.ResolveSystemById(b.Value) ?? b.Value.ToString();
                conflicts.Add($"{aName} ↔ {bName}（端已占用）");
            }
        }

        var builder = new StringBuilder();
        builder.AppendLine($"导入完成：新增 {added} 组");
        if (conflicts.Count > 0)
        {
            builder.AppendLine($"冲突跳过 {conflicts.Count} 组：" + string.Join("；", conflicts.Take(8)));
        }

        if (skipped.Count > 0)
        {
            builder.AppendLine($"未识别跳过 {skipped.Count} 组：" + string.Join("；", skipped.Take(8)));
        }

        return Task.FromResult(new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"导入 {bridges.Count} 组（新增 {added}）"));
    }

    /// <summary>解析跳桥一端：先按星系名，失败时按数字 ID。</summary>
    private static int? ResolveEnd(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var system = ToolNameResolver.ResolveSystem(trimmed);
        if (system is not null)
        {
            return system.SolarSystemID;
        }

        return int.TryParse(trimmed, out var id) && id > 0 ? id : null;
    }
}
