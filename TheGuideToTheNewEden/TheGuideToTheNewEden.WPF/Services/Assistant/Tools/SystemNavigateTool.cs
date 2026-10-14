using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>跳到星图并定位到指定星系（动作；唯一命中才跳，歧义/未命中只给原因）。</summary>
public sealed class SystemNavigateTool : ITool
{
    public string Name => "system_navigate";

    public string DisplayNameKey => "AssistantTool_system_navigate";

    public string Description =>
        "打开软件星图并定位到指定星系（配合「某某在哪」类问题使用）。参数 systemName 用星系名（如 吉他 / Jita）。" +
        "找不到或名称有歧义时返回原因、不跳转。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["systemName"] = new JsonObject { ["type"] = "string", ["description"] = "星系名（如 吉他 / Jita）" },
        },
        ["required"] = new JsonArray { "systemName" },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => true;

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var systemName = ToolArgs.ReadString(args, "systemName");
        if (string.IsNullOrWhiteSpace(systemName))
        {
            return Task.FromResult(new ToolResult("参数 systemName 不能为空。"));
        }

        var system = ToolNameResolver.ResolveSystem(systemName);
        if (system is null)
        {
            return Task.FromResult(new ToolResult($"未找到星系「{systemName}」。"));
        }

        var id = system.SolarSystemID;
        return Task.FromResult(new ToolResult(
            $"已打开星图并定位到 {system.SolarSystemName}（星系 ID {id}）。",
            NavigateAction: () => Navigation.NavigateToSystem(id),
            ParamSummary: system.SolarSystemName));
    }
}
