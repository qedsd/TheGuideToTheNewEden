using TheGuideToTheNewEden.WPF.Services.Translation.Llm;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>
/// 工具注册表（静态注册）：<c>Name → ITool</c> + 给模型的 <see cref="ChatTool"/> 声明。
/// <para>
/// 静态构造函数注册内置工具（服务层惯例：静态类 + 静态注册）；
/// 新工具（星图/商业等）只需在 <see cref="ToolRegistry"/> 静态构造函数里加一行。
/// </para>
/// </summary>
public static class ToolRegistry
{
    private static readonly Dictionary<string, ITool> Tools = new(StringComparer.Ordinal);

    static ToolRegistry()
    {
        // 阶段 B：3 个代表工具
        Register(new Tools.MarketOrdersTool());
        Register(new Tools.TranslateTool());
        Register(new Tools.SystemNavigateTool());
        // 阶段 C：通用 9 个
        Register(new Tools.AppraisalTool());
        Register(new Tools.MarketNavigateTool());
        Register(new Tools.CharacterListTool());
        Register(new Tools.CharacterOverviewTool());
        Register(new Tools.CharacterWalletTool());
        Register(new Tools.CharacterSkillsTool());
        Register(new Tools.CharacterNavigateTool());
        Register(new Tools.SearchSdeTool());
        Register(new Tools.MarketHistoryTool());
        // 阶段 C2：星图 7 个
        Register(new Tools.MapRouteTool());
        Register(new Tools.MapSystemInfoTool());
        Register(new Tools.MapJumpBridgeListTool());
        Register(new Tools.MapJumpBridgeAddTool());
        Register(new Tools.MapJumpBridgeRemoveTool());
        Register(new Tools.MapJumpBridgeImportTool());
        Register(new Tools.MapLayerTool());
        // 阶段 C3：商业 4 个
        Register(new Tools.ScalperSettingsTool());
        Register(new Tools.ScalperAnalyseTool());
        Register(new Tools.CompareAnalyseTool());
        Register(new Tools.MarketTrendTool());
        // 阶段 114：v1.5 P1 九个（订单/购买文本/邮件/合同/工业/位置/ZKB/DED/虫洞）
        Register(new Tools.CharacterOrdersTool());
        Register(new Tools.BuildBuyOrderTextTool());
        Register(new Tools.CharacterMailTool());
        Register(new Tools.CharacterContractsTool());
        Register(new Tools.CharacterIndustryTool());
        Register(new Tools.CharacterLocationTool());
        Register(new Tools.ZkbEntityTool());
        Register(new Tools.DedInfoTool());
        Register(new Tools.WormholeInfoTool());
    }

    public static void Register(ITool tool)
    {
        Tools[tool.Name] = tool;
    }

    public static ITool? Find(string name)
        => Tools.TryGetValue(name, out var tool) ? tool : null;

    public static IReadOnlyList<ITool> All => Tools.Values.OrderBy(t => t.Name).ToList();

    /// <summary>给模型的 tools 声明（Name/Description/Parameters 的协议无关形状，各适配器转自己的格式）。</summary>
    public static IReadOnlyList<ChatTool> AllSchemas
        => All.Select(t => new ChatTool
        {
            Name = t.Name,
            Description = t.Description,
            Parameters = t.Parameters,
        }).ToList();
}
