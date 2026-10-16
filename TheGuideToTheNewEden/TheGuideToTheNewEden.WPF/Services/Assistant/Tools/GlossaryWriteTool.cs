using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Translation;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 用户术语表写入（**写操作，需确认**）：新增/更新一条术语（英文名唯一，忽略大小写），或删除一条。
/// <para>
/// 术语表的语义见 <see cref="UserGlossaryService"/>：它压过 SDE 术语库、参与 AI 翻译的提示词注入与
/// 术语后校验，因此"加错一条术语"会让后续翻译持续用错词——这类改动必须由用户确认。
/// 一次调用只改一条：命中的是"写盘"而不是"批量整理"（批量请用「设置 → 术语表」页）。
/// </para>
/// </summary>
public sealed class GlossaryWriteTool : ITool
{
    public string Name => "glossary_write";

    public string DisplayNameKey => "AssistantTool_glossary_write";

    public string Description =>
        "维护用户自定义术语表（优先级高于内置 SDE 术语库，翻译时强制按它译）：" +
        "给英文名与中文译名即可新增或覆盖，也可按英文名删除。" +
        "适合「以后把 Xxx 译成 Yyy」「记下这条译名」。写操作需用户确认，一次仅一条。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["action"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "add", "remove" }, ["description"] = "add（默认）新增/覆盖；remove 删除" },
            ["english"] = new JsonObject { ["type"] = "string", ["description"] = "英文名（术语原文，唯一键）" },
            ["chinese"] = new JsonObject { ["type"] = "string", ["description"] = "中文译名（action=add 时必填）" },
            ["note"] = new JsonObject { ["type"] = "string", ["description"] = "可选备注（来源/原因）" },
            ["pinned"] = new JsonObject { ["type"] = "boolean", ["description"] = "true（默认）= 要求模型原样使用该译名" },
        },
        ["required"] = new JsonArray { "english" },
    };

    public bool RequiresConfirmation => true;

    public bool IsAction => false;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var action = (ToolArgs.ReadString(args, "action") ?? "add").Trim().ToLowerInvariant();
        var english = ToolArgs.ReadString(args, "english")?.Trim();
        var chinese = ToolArgs.ReadString(args, "chinese")?.Trim();

        if (string.IsNullOrWhiteSpace(english))
        {
            return Task.FromResult(new ToolResult("参数 english（英文名）不能为空。"));
        }

        if (action == "remove")
        {
            var existing = UserGlossaryService.FindByEnglish(english);
            if (existing is null)
            {
                return Task.FromResult(new ToolResult($"术语表里没有英文名「{english}」的条目，无需删除。"));
            }

            var removed = UserGlossaryService.Remove(english);
            return Task.FromResult(new ToolResult(
                removed
                    ? $"已从用户术语表删除：{english} → {existing.Chinese}（现有 {UserGlossaryService.Count} 条）。"
                    : $"删除「{english}」失败，请到「设置 → 术语表」页确认。",
                ParamSummary: $"删除术语 {english}"));
        }

        if (string.IsNullOrWhiteSpace(chinese))
        {
            return Task.FromResult(new ToolResult("新增术语需要同时给出 english（英文名）与 chinese（中文译名）。"));
        }

        var before = UserGlossaryService.FindByEnglish(english);
        var note = ToolArgs.ReadString(args, "note");
        var pinned = ToolArgs.ReadBool(args, "pinned", true);

        if (!UserGlossaryService.Add(english, chinese, note, pinned))
        {
            return Task.FromResult(new ToolResult($"写入术语「{english} → {chinese}」失败（英文名或译名为空），请到「设置 → 术语表」页重试。"));
        }

        var verb = before is null ? "已新增" : $"已更新（原译名 {before.Chinese}）";
        var builder = new StringBuilder();
        builder.AppendLine($"{verb}用户术语：{english} → {chinese}{(pinned ? "（固定译名）" : string.Empty)}");
        builder.AppendLine($"术语表现有 {UserGlossaryService.Count} 条；后续翻译会自动使用该译名（AI 结果缓存已随之失效）。");

        return Task.FromResult(new ToolResult(builder.ToString().TrimEnd(),
            ParamSummary: $"{(before is null ? "新增" : "更新")}术语 {english} → {chinese}"));
    }
}
