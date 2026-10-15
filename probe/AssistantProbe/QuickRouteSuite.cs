using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.Services.Home;
using TheGuideToTheNewEden.WPF.ViewModels.Assistant;

namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// 主页快捷输入的路由（阶段 110 + 113 补测）：本地确定性判定 → 直达跳转 / 转助手；
/// 以及 <c>Navigation</c> 的 pending 通道语义（**消费一次**，这是"事件通道与 pending 双路径不重复发送"的基础）。
/// <para>
/// **不发任何 LLM 请求**：助手页消费 pending 后会自动 submit，可能真的打模型端点（烧钱），
/// 因此只在端点未配置时才跑那段断言，否则明确跳过并提示由实机确认。
/// </para>
/// </summary>
internal static class QuickRouteSuite
{
    public static void Run(Check c)
    {
        // 路由要用本地库解析物品名/星系名（ToolNameResolver → MarketTypeNameResolver / MapSolarSystemService），
        // 所以必须先初始化 Core：阶段 116 单独跑本套件时这里漏了，直接 NRE（`all` 里被 tools 套件的初始化掩盖）。
        ProbeCore.Ensure();
        ProbeApplication.Ensure();   // 语言键要能解析，toast 文案才可断言

        c.Section("QuickRouteSuite · 空输入与兜底");
        EmptyAndFallback(c);

        c.Section("QuickRouteSuite · 直达跳转（价格 / 星系 / 角色）");
        DirectJumps(c);

        c.Section("QuickRouteSuite · 转助手与 pending 消费语义");
        ToAssistant(c);

        c.Section("QuickRouteSuite · 助手页消费 pending（仅在端点未配置时执行）");
        ViewModelConsumption(c);
    }

    private static void EmptyAndFallback(Check c)
    {
        foreach (var input in new[] { null, "", "   ", "\t" })
        {
            DrainAll();
            var result = HomeQuickQueryRouter.RouteAsync(input).GetAwaiter().GetResult();
            c.Equal(result.Kind, HomeQuickQueryRouter.RouteKind.None, $"空输入（{Describe(input)}）不做任何跳转");
        }

        // 只有意图关键词、没有实体名 → 不能跳转，转助手
        DrainAll();
        var onlyKeyword = HomeQuickQueryRouter.RouteAsync("价格").GetAwaiter().GetResult();
        c.Equal(onlyKeyword.Kind, HomeQuickQueryRouter.RouteKind.ToAssistant, "只有关键词「价格」没有实体名 → 转助手（不瞎跳）");
        c.That(Navigation.TakePendingAssistantQuery() is { Length: > 0 }, "转助手时留下了待处理提问（供助手页自动发送）");
    }

    private static void DirectJumps(Check c)
    {
        var item = ToolNameResolver.ResolveItem("巨神兵级");
        if (item is not null)
        {
            DrainAll();
            var result = HomeQuickQueryRouter.RouteAsync("巨神兵级价格").GetAwaiter().GetResult();
            c.Equal(result.Kind, HomeQuickQueryRouter.RouteKind.Jumped, "「巨神兵级价格」直接跳转");
            c.Equal(Navigation.TakePendingMarketType(), (long?)item.TypeID, "跳转目标 = 该物品的市场页");
            c.That(result.Message?.Contains(item.TypeName, StringComparison.Ordinal) == true,
                $"toast 文案带物品名（实际「{result.Message}」）");
            c.That(Navigation.TakePendingAssistantQuery() is null, "直达跳转**不**留下助手待处理提问");

            // 带语气词的写法：先剥意图关键词，再剥"查一下/多少钱"
            DrainAll();
            var chatty = HomeQuickQueryRouter.RouteAsync("查一下 巨神兵级 多少钱").GetAwaiter().GetResult();
            c.Equal(chatty.Kind, HomeQuickQueryRouter.RouteKind.Jumped, "「查一下 巨神兵级 多少钱」同样能直达");
            c.Equal(Navigation.TakePendingMarketType(), (long?)item.TypeID, "语气词不影响实体解析结果");
        }
        else
        {
            c.That(false, "本地库解析不到「巨神兵级」——物品库未就绪，后续价格路由断言无法进行");
        }

        var system = ToolNameResolver.ResolveSystem("吉他");
        if (system is not null)
        {
            DrainAll();
            var result = HomeQuickQueryRouter.RouteAsync("定位 吉他").GetAwaiter().GetResult();
            c.Equal(result.Kind, HomeQuickQueryRouter.RouteKind.Jumped, "「定位 吉他」直接跳转（中文星系名必须能解析）");
            c.Equal(Navigation.TakePendingSystem(), (int?)system.SolarSystemID, "跳转目标 = 该星系");

            DrainAll();
            var suffix = HomeQuickQueryRouter.RouteAsync("吉他 在哪").GetAwaiter().GetResult();
            c.Equal(suffix.Kind, HomeQuickQueryRouter.RouteKind.Jumped, "关键词在句尾（「吉他 在哪」）也能直达");
        }
        else
        {
            c.That(false, "中文星系名「吉他」解析失败——ToolNameResolver 的本地化名解析回归了");
        }

        // 角色全名：有授权角色才测"直达"，否则验证"无角色时转助手"
        var character = CharacterStore.Characters.FirstOrDefault();
        DrainAll();
        if (character is not null)
        {
            var result = HomeQuickQueryRouter.RouteAsync(character.CharacterName).GetAwaiter().GetResult();
            c.Equal(result.Kind, HomeQuickQueryRouter.RouteKind.Jumped, $"角色全名「{character.CharacterName}」直达角色页");
            c.Equal(Navigation.TakePendingCharacter(), (long?)character.CharacterID, "跳转目标 = 该角色");
        }
        else
        {
            var result = HomeQuickQueryRouter.RouteAsync("某个角色名").GetAwaiter().GetResult();
            c.Equal(result.Kind, HomeQuickQueryRouter.RouteKind.ToAssistant, "没有已授权角色时，像角色名的输入转助手");
        }
    }

    private static void ToAssistant(Check c)
    {
        foreach (var input in new[] { "绝不存在的东西XYZ 价格", "绝不存在星系XYZ 定位", "帮我分析一下这个星域的倒货机会" })
        {
            DrainAll();
            var result = HomeQuickQueryRouter.RouteAsync(input).GetAwaiter().GetResult();
            c.Equal(result.Kind, HomeQuickQueryRouter.RouteKind.ToAssistant, $"「{input}」转助手");
            c.Equal(Navigation.TakePendingAssistantQuery(), input, "待处理提问 = 原始输入（一字不改地交给助手）");

            // 消费一次：第二次必须是 null——这是"页面已建（事件通道）/ 未建（pending）"两条路径不重复发送的基础
            c.That(Navigation.TakePendingAssistantQuery() is null, $"「{input}」的待处理提问只能被消费一次");
        }
    }

    private static void ViewModelConsumption(Check c)
    {
        var viewModel = new AiAssistantViewModel();
        if (viewModel.IsConfigured)
        {
            c.That(true, "端点已配置 → 跳过该段（助手页 Init 会真的发模型请求，改由实机确认）");
            DrainAll();
            return;
        }

        DrainAll();
        HomeQuickQueryRouter.RouteAsync("帮我看看巨神兵级的价格走势").GetAwaiter().GetResult();
        c.That(Navigation.TakePendingAssistantQuery() is { Length: > 0 }, "（前置）已留下待处理提问");

        // 再留一次，让 Init 去消费
        HomeQuickQueryRouter.RouteAsync("帮我看看巨神兵级的价格走势").GetAwaiter().GetResult();
        viewModel.Init();
        c.That(Navigation.TakePendingAssistantQuery() is null, "助手页 Init 消费掉了待处理提问（不会与事件通道重复发送）");
        c.That(viewModel.InputText?.Contains("巨神兵级", StringComparison.Ordinal) == true,
            $"提问已进入输入框（实际「{viewModel.InputText}」）");
        viewModel.Dispose();
    }

    // ---------- 工具方法 ----------

    /// <summary>清空所有 pending 通道，保证用例之间互不干扰。</summary>
    private static void DrainAll()
    {
        Navigation.TakePendingMarketType();
        Navigation.TakePendingSystem();
        Navigation.TakePendingCharacter();
        Navigation.TakePendingAssistantQuery();
        Navigation.TakePendingMapCommand();
    }

    private static string Describe(string? text) => text is null ? "null" : $"\"{text}\"";
}
