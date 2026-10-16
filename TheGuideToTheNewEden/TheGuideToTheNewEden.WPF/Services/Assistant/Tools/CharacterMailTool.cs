using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>角色邮件：标签（含未读数）+ 某个标签下的邮件列表。</summary>
public sealed class CharacterMailTool : ITool
{
    public string Name => "character_mail";

    public string DisplayNameKey => "AssistantTool_character_mail";

    public string Description =>
        "查角色邮件：先给标签与未读数，再列出该标签下邮件的标题、发件人、时间与已读状态。" +
        "只读，不会标记已读，需邮件权限。适合「我有多少未读邮件」「最近谁给我发邮件」。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名，省略 = 默认角色" },
            ["labelId"] = new JsonObject { ["type"] = "integer", ["description"] = "标签 Id，省略 = 全部" },
            ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "条数，默认 12，上限 40" },
        },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var (characterCtx, error) = await CharacterToolHelper.ResolveAsync(ctx, ToolArgs.ReadString(args, "characterName"), ct);
        if (error is not null)
        {
            return new ToolResult(error);
        }

        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 12), 1, 40);
        var labelId = ToolArgs.ReadInt(args, "labelId", 0) > 0 ? ToolArgs.ReadInt(args, "labelId", 0) : (long?)null;

        var labels = await CharacterMailService.GetLabelsAsync(characterCtx!, false);
        var headers = await CharacterMailService.GetHeadersAsync(characterCtx!, labelId);
        if (headers is null)
        {
            return new ToolResult("邮件获取失败（ESI 不可用、缺少邮件权限或限流）。");
        }

        var builder = new StringBuilder();
        if (labels is { Count: > 0 })
        {
            var unreadTotal = labels.Sum(l => l.UnreadCount);
            builder.AppendLine($"标签未读合计 {unreadTotal} 封：");
            foreach (var label in labels.OrderByDescending(l => l.UnreadCount).Take(10))
            {
                builder.AppendLine($"- {label.Name}（Id {label.LabelId}，未读 {label.UnreadCount}）");
            }
        }

        if (headers.Count == 0)
        {
            builder.AppendLine("该范围内没有邮件。");
            return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: "无邮件");
        }

        builder.AppendLine($"邮件共 {headers.Count} 封，显示前 {Math.Min(top, headers.Count)} 封：");
        foreach (var mail in headers.Take(top))
        {
            var unread = mail.IsRead ? string.Empty : "【未读】";
            builder.AppendLine($"- {unread}{mail.Date:MM-dd HH:mm}｜{mail.FromName}｜{mail.Subject}（Id {mail.MailId}）");
        }

        return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"{headers.Count} 封邮件");
    }
}
