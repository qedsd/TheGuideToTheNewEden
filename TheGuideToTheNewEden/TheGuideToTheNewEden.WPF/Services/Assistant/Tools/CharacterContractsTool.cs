using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>角色（或军团）合同列表：类型、价格、对方、状态、到期时间。</summary>
public sealed class CharacterContractsTool : ITool
{
    public string Name => "character_contracts";

    public string DisplayNameKey => "AssistantTool_character_contracts";

    public string Description =>
        "查角色或军团的合同列表（只读）：编号、标题、类型、价格、发布者/接受者、状态、发布与到期时间。" +
        "省略 = 默认角色；可查军团合同（需军团合同权限）；支持翻页与条数上限。" +
        "适合「我有没有没处理的合同」「军团合同里有哪些在等」。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["characterName"] = new JsonObject { ["type"] = "string", ["description"] = "角色全名（省略 = 默认角色）" },
            ["corporation"] = new JsonObject { ["type"] = "boolean", ["description"] = "true = 查军团合同（默认个人）" },
            ["page"] = new JsonObject { ["type"] = "integer", ["description"] = "页码（默认 1）" },
            ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "条数（默认 12，上限 40）" },
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

        var corporation = ToolArgs.ReadBool(args, "corporation", false);
        var page = Math.Max(1, ToolArgs.ReadInt(args, "page", 1));
        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 12), 1, 40);

        var result = await CharacterContractService.GetAsync(characterCtx!, corporation, page, false);
        if (result is null)
        {
            return new ToolResult(corporation
                ? "军团合同获取失败（需要军团合同权限，或 ESI 不可用/限流）。"
                : "合同获取失败（ESI 不可用、缺少合同权限或限流）。");
        }

        if (result.Items.Count == 0)
        {
            return new ToolResult($"第 {page} 页没有{(corporation ? "军团" : "")}合同。");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"第 {page} 页{(corporation ? "军团" : "个人")}合同 {result.Items.Count} 条（还有下一页：{(result.HasNextPage ? "是" : "否")}）：");
        foreach (var contract in result.Items.Take(top))
        {
            var expired = contract.DateExpired is { } end ? $"｜到期 {end:MM-dd HH:mm}" : string.Empty;
            var acceptor = string.IsNullOrWhiteSpace(contract.AcceptorName) ? string.Empty : $"｜接受者 {contract.AcceptorName}";
            builder.AppendLine(
                $"- [{contract.Status}] {contract.TypeText}｜{(string.IsNullOrWhiteSpace(contract.Title) ? $"合同 {contract.ContractId}" : contract.Title)}｜" +
                $"{ToolArgs.FormatIsk(contract.Price)} ISK｜发布 {contract.IssuerName}｜{contract.DateIssued:MM-dd HH:mm}{expired}{acceptor}");
        }

        return new ToolResult(builder.ToString().TrimEnd(), ParamSummary: $"第 {page} 页 {result.Items.Count} 条合同");
    }
}
