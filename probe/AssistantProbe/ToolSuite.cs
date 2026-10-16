using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Services.Translation;

namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// 工具层：Schema 合法性（每个工具都有可被 LLM 理解的描述与合规参数 schema）、
/// 写/重型工具的确认标记、导航工具的动作标记，以及**确定性执行**（走真 SDE 库、不依赖网络的用例）。
/// </summary>
internal static class ToolSuite
{
    /// <summary>必须有确认门控的工具（写操作 + 重型取数）。</summary>
    private static readonly string[] ConfirmationTools =
        ["map_jumpbridge_add", "map_jumpbridge_remove", "map_jumpbridge_import", "scalper_analyse", "compare_analyse",
         "glossary_write", "market_favorite", "shopping_cart",
         // 阶段 119：ESI 通用网关的写入口（确认卡片显示 IPendingSummary 给出的人话摘要）
         "esi_write"];

    /// <summary>必须标记为"动作"的工具（结果只想跳页面）。</summary>
    private static readonly string[] ActionTools =
        ["market_navigate", "system_navigate", "character_navigate", "map_route", "map_layer", "links_search"];

    /// <summary>纯只读工具：**不得**要求确认（确认是给写操作/重型取数用的）。</summary>
    private static readonly string[] ReadOnlyTools =
        ["character_orders", "build_buy_order_text", "character_mail", "character_contracts",
         "character_industry", "character_location", "zkb_entity", "ded_info", "wormhole_info",
         "server_ping", "channel_scan", "trade_spread", "mission_info",
         // 阶段 119：esi_* 四个元工具（esi_read 只读；esi_write 需要确认，见 ConfirmationTools）
         "esi_search", "esi_describe", "esi_read"];

    public static void Run(Check c)
    {
        c.Section("ToolSuite · 初始化（真 SDE 库）");
        ProbeCore.Ensure();
        c.That(CoreInitializer.DatabaseReady, "Core 初始化：数据库就绪");

        c.Section("ToolSuite · Schema 合法性（全部工具）");
        Schema(c);

        c.Section("ToolSuite · 确定性执行（本地库，无网络）");
        Execute(c);

        c.Section("ToolSuite · 逐工具开关（阶段 121）");
        ToolSwitches(c);
    }

    /// <summary>
    /// 逐工具开关：纯过滤函数 + 一次真实写盘的端到端（**先备份 settings.json 字节、跑完还原**，
    /// 内存里的禁用集合也还原成进套件前的样子，避免影响后续套件与用户配置）。
    /// </summary>
    private static void ToolSwitches(Check c)
    {
        var all = ToolRegistry.All;
        var productTools = all.Where(t => !t.Name.StartsWith("probe_", StringComparison.Ordinal)).ToList();
        c.Equal(productTools.Count, 44, "产品工具 44 个（全部列进设置页开关）");

        // 纯函数：过滤语义
        var disabled = new HashSet<string>(StringComparer.Ordinal) { "map_route", "esi_write" };
        var schemas = ToolRegistry.FilterSchemas(all, disabled);
        c.Equal(schemas.Count, all.Count - 2, "禁用 2 个 → 声明少 2 条");
        c.That(schemas.All(s => s.Name is not ("map_route" or "esi_write")), "被关的工具不在给模型的声明里");
        c.That(schemas.All(s => !string.IsNullOrWhiteSpace(s.Description)),
            "过滤后每条声明仍完整（描述都在；Parameters 允许为 null —— 工具声明里 null 就是「无参数」）");
        c.That(schemas.All(s => all.Any(t => t.Name == s.Name)), "过滤后只做减法，不改动工具本身");
        c.Equal(ToolRegistry.FilterSchemas(all, null).Count, all.Count, "禁用集合为 null = 全开");
        c.Equal(ToolRegistry.FilterSchemas(all, new HashSet<string>(StringComparer.Ordinal)).Count, all.Count, "空集合 = 全开");

        // 端到端：设置 → 注册表（写盘；跑完按字节还原）
        var settingsFile = Path.Combine(SettingsService.DataPath, "settings.json");
        var backup = File.Exists(settingsFile) ? File.ReadAllBytes(settingsFile) : null;
        var before = AiAssistantSettingService.DisabledTools.ToHashSet(StringComparer.Ordinal);

        try
        {
            AiAssistantSettingService.SaveDisabledTools(["map_route"]);
            c.That(!AiAssistantSettingService.IsToolEnabled("map_route"), "保存后该工具被标记为关闭");
            c.That(AiAssistantSettingService.IsToolEnabled("map_system_info"), "其它工具不受影响");
            c.That(ToolRegistry.EnabledSchemas.All(s => s.Name != "map_route"), "EnabledSchemas 不下发被关的工具");
            c.That(ToolRegistry.Enabled.All(t => t.Name != "map_route"), "Enabled 列表里没有它");
            c.That(ToolRegistry.IsEnabled("map_route") == false, "IsEnabled 反映开关（执行侧据此拒绝）");

            AiAssistantSettingService.SaveDisabledTools(null);
            c.That(AiAssistantSettingService.IsToolEnabled("map_route"), "清空禁用集合后恢复启用");
        }
        finally
        {
            AiAssistantSettingService.SaveDisabledTools(before);
            if (backup is not null)
            {
                File.WriteAllBytes(settingsFile, backup);
            }
        }

        c.That(AiAssistantSettingService.DisabledTools.Count == before.Count,
            $"跑完还原成进套件前的禁用集合（{before.Count} 个）");
        c.That(ToolRegistry.FilterSchemas(all, before).Count == all.Count - before.Count,
            "按还原后的集合过滤，条数自洽");
    }

    private static void Schema(Check c)
    {
        var tools = ToolRegistry.All;
        var productTools = tools.Where(t => !t.Name.StartsWith("probe_", StringComparison.Ordinal)).ToList();
        c.That(productTools.Count >= 40, $"已注册的产品工具数 ≥ 40（实际 {productTools.Count}，含探针共 {tools.Count}）");
        c.That(ToolRegistry.AllSchemas.Count == tools.Count, "给模型的声明数与注册表一致");

        foreach (var tool in tools)
        {
            var name = tool.Name;
            c.That(!string.IsNullOrWhiteSpace(name) && name.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_'),
                $"{name}：名字是 LLM 友好的英文 snake_case");
            c.That(!string.IsNullOrWhiteSpace(tool.DisplayNameKey), $"{name}：有界面本地化键");

            // 描述是否"足够具体"只对**产品工具**要求（探针自己的 probe_* 不面向真实模型）
            if (!name.StartsWith("probe_", StringComparison.Ordinal))
            {
                c.That(!string.IsNullOrWhiteSpace(tool.Description) && tool.Description.Length >= 20,
                    $"{name}：描述足够具体（模型靠它选工具）");
            }

            if (tool.Parameters is not { } parameters)
            {
                continue;
            }

            c.Equal(parameters["type"]?.GetValue<string>(), "object", $"{name}：参数 schema 是 object 型");
            var properties = parameters["properties"] as JsonObject;
            c.That(properties is { Count: > 0 }, $"{name}：schema 有 properties");
            if (parameters["required"] is JsonArray required && properties is not null)
            {
                foreach (var item in required)
                {
                    var requiredName = item?.GetValue<string>() ?? "?";
                    c.That(properties.ContainsKey(requiredName), $"{name}：required 的 {requiredName} 在 properties 里");
                }
            }
        }

        foreach (var name in ConfirmationTools)
        {
            c.That(ToolRegistry.Find(name)?.RequiresConfirmation == true, $"{name}：RequiresConfirmation=true（写/重型操作必须确认）");
        }

        foreach (var name in ActionTools)
        {
            c.That(ToolRegistry.Find(name)?.IsAction == true, $"{name}：IsAction=true（结果用于跳页面）");
        }

        foreach (var name in ReadOnlyTools)
        {
            var tool = ToolRegistry.Find(name);
            c.That(tool is not null, $"{name}：已注册（v1.5 P1）");
            c.That(tool?.RequiresConfirmation == false, $"{name}：只读工具不要求确认");
        }

        // 工具的中文名本地化键覆盖由 UiSuite 检查（那里才载入了应用资源字典）
    }

    private static void Execute(Check c)
    {
        var ctx = new ToolContext
        {
            Character = null,
            Characters = [],
            OnUiThread = action => action(),
            Progress = _ => { },
        };

        // 翻译（本地词库；未启用本地化库时如实跳过）
        if (TranslationService.LocalDatabase.IsAvailable)
        {
            Exec(c, ctx, "translate", """{"text":"Rifter"}""", text => text.Contains("裂谷级"), "translate：本地词库 Rifter → 裂谷级");
        }
        else
        {
            c.That(true, "translate：本地化库未启用，跳过正例（在「设置 → 一般」启用后可复核）");
        }

        Exec(c, ctx, "translate", """{"text":""}""", text => text.Contains("请输入"), "translate：空输入给出明确原因");
        Exec(c, ctx, "translate", """{"text":"Rifter","direction":"en_zh"}""", text => text.Length > 0, "translate：显式方向可执行");

        // SDE 检索
        Exec(c, ctx, "search_sde", """{"keyword":"Rifter"}""", text => text.Contains("Rifter"), "search_sde：检索到 Rifter");
        Exec(c, ctx, "search_sde", """{"keyword":"吉他","category":"solar_system"}""", text => text.Length > 0, "search_sde：按星系类别过滤");
        Exec(c, ctx, "search_sde", """{"keyword":"绝不存在的东西XYZ"}""", text => text.Contains("没有找到"), "search_sde：未命中给出原因");

        // 市场类：物品解析走本地库，**不需要网络**就能验证"解析失败给原因"
        Exec(c, ctx, "market_orders", """{}""", text => text.Contains("不能为空"), "market_orders：缺参给出原因");
        Exec(c, ctx, "market_orders", """{"itemName":"绝不存在的东西XYZ"}""", text => text.Contains("未找到物品"), "market_orders：物品不存在给出原因（不发起 ESI）");
        Exec(c, ctx, "market_orders", """{"itemName":"Rifter","regionName":"绝不存在星域XYZ"}""", text => text.Contains("未找到星域"), "market_orders：星域不存在给出原因");
        Exec(c, ctx, "market_trend", """{"itemName":"绝不存在的东西XYZ"}""", text => text.Contains("未找到物品"), "market_trend：物品不存在给出原因");
        Exec(c, ctx, "appraisal", """{"text":""}""", text => text.Contains("不能为空"), "appraisal：空清单给出原因");
        Exec(c, ctx, "appraisal", """{"text":"绝不存在的东西XYZ 1"}""", text => text.Length > 0, "appraisal：无法识别的清单不崩（给出结果或提示）");

        // 星图 / 角色
        // 注意断言标记：成功回执里才有「安等」（未找到的回执会把输入名回显，只比名字会**假通过**，
        // 阶段 114 就是靠这条发现"中文星系名解析不出来"的真 bug）
        Exec(c, ctx, "map_system_info", """{"systemName":"吉他"}""",
            text => text.Contains("安等"), "map_system_info：中文星系名解析成功（含安等）");
        Exec(c, ctx, "map_system_info", """{"systemName":"Jita"}""",
            text => text.Contains("安等"), "map_system_info：英文原名同样解析成功");
        Exec(c, ctx, "map_system_info", """{"systemName":"绝不存在星系XYZ"}""",
            text => text.Contains("未找到星系"), "map_system_info：未找到给出原因");
        Exec(c, ctx, "system_navigate", """{"systemName":"绝不存在星系XYZ"}""",
            text => text.Contains("未找到星系"), "system_navigate：未找到不跳转");

        // 中文名的"正向"路径：解析成功必须带 NavigateAction（否则只是嘴上说定位了）
        var navigate = ExecResult(c, ctx, "system_navigate", """{"systemName":"吉他"}""", "system_navigate：中文星系名正向定位");
        c.That(navigate?.NavigateAction is not null, "system_navigate：成功时给出跳转动作（不只是文本）");
        navigate?.NavigateAction?.Invoke();
        c.Equal(Navigation.TakePendingSystem(), ToolNameResolver.ResolveSystem("吉他")?.SolarSystemID,
            "跳转动作落到待定位星系通道，且与解析出的星系一致");

        Exec(c, ctx, "market_navigate", """{"itemName":"绝不存在的东西XYZ"}""",
            text => text.Contains("未找到物品"), "market_navigate：未找到不跳转");
        Exec(c, ctx, "map_jumpbridge_list", """{}""", text => text.Length > 0, "map_jumpbridge_list：给出列表或「未配置」提示");
        Exec(c, ctx, "map_route", """{"from":"绝不存在星系XYZ","to":"吉他"}""", text => text.Contains("未找到星系"), "map_route：端点未找到 → 不画图");
        Exec(c, ctx, "character_list", """{}""", text => text.Length > 0, "character_list：给出角色或「先添加角色」提示");
        Exec(c, ctx, "character_wallet", """{}""", text => text.Length > 0, "character_wallet：无角色时给出明确原因");
        Exec(c, ctx, "scalper_settings", """{}""", text => text.Length > 0, "scalper_settings：给出当前倒货设置摘要");

        // ---------- 阶段 114：v1.5 P1 九个工具 ----------
        // 角色类：用"不存在的角色名"走确定性分支（不碰 ESI），验证错误原因清晰
        foreach (var name in new[] { "character_orders", "build_buy_order_text", "character_mail", "character_contracts", "character_industry" })
        {
            Exec(c, ctx, name, """{"characterName":"绝不存在角色XYZ"}""",
                text => text.Contains("未找到角色"), $"{name}：角色不存在 → 明确原因（不发 ESI）");
        }

        Exec(c, ctx, "character_orders", """{}""",
            text => text.Contains("未登录任何角色"), "character_orders：无默认角色 → 明确原因");
        Exec(c, ctx, "character_mail", """{"top":5}""",
            text => text.Contains("未登录任何角色"), "character_mail：无默认角色 → 明确原因");

        Exec(c, ctx, "zkb_entity", """{}""", text => text.Contains("不能为空"), "zkb_entity：缺 entityName 给出原因");
        Exec(c, ctx, "zkb_entity", """{"entityName":"   "}""", text => text.Contains("不能为空"), "zkb_entity：空白实体名同样被拒");

        Exec(c, ctx, "ded_info", """{}""", text => text.Contains("请给 keyword"), "ded_info：既无关键词也无势力 → 明确原因");
        Exec(c, ctx, "ded_info", """{"faction":"喵星人"}""", text => text.Contains("不认识的势力"), "ded_info：未知势力 → 列出支持值");
        Exec(c, ctx, "ded_info", """{"faction":"天蛇","level":6,"top":5}""",
            text => text.Contains("天蛇") || text.Contains("没有找到"), "ded_info：本地库按势力+等级查询");

        Exec(c, ctx, "wormhole_info", """{}""", text => text.Contains("不能为空"), "wormhole_info：缺 keyword 给出原因");
        Exec(c, ctx, "wormhole_info", """{"keyword":"J244"}""",
            text => text.Contains("J244") || text.Contains("洞系") || text.Contains("没有匹配"), "wormhole_info：本地库洞系查询");
        Exec(c, ctx, "wormhole_info", """{"keyword":"绝不存在洞口XYZ"}""",
            text => text.Contains("没有匹配"), "wormhole_info：未命中 → 明确原因");

        // character_location 会真的打 ESI 取实时位置（有授权角色时）：只要求"不崩、有输出"
        Exec(c, ctx, "character_location", """{"characterName":"绝不存在角色XYZ"}""",
            text => text.Length > 0, "character_location：不存在的角色名不崩（给出结果或原因）");

        // ---------- 阶段 116：v1.5 P2 五个只读 ----------
        // 全部只走"确定性失败分支"，不依赖网络与用户配置（成功的取数路径由 WriteToolSuite/实机覆盖）
        Exec(c, ctx, "server_ping", """{"port":0}""",
            text => text.Contains("端口"), "server_ping：非法端口 → 明确原因（不发连接）");
        Exec(c, ctx, "server_ping", """{"host":" ","port":70000}""",
            text => text.Contains("端口"), "server_ping：空白主机名 + 越界端口仍给明确原因");

        Exec(c, ctx, "channel_scan", """{"names":[]}""",
            text => text.Contains("不能为空"), "channel_scan：空名单 → 明确原因");
        Exec(c, ctx, "channel_scan", """{"names":["  ",""]}""",
            text => text.Contains("不能为空"), "channel_scan：全空白名同样被拒");
        c.That(ToolRegistry.Find("channel_scan")?.Description.Contains("军团") == true,
            "channel_scan：描述里写清返回军团/联盟归属（模型靠它选工具）");

        Exec(c, ctx, "links_search", """{}""",
            text => text.Contains("不能为空"), "links_search：缺 keyword → 明确原因");
        Exec(c, ctx, "links_search", """{"keyword":"   "}""",
            text => text.Contains("不能为空"), "links_search：空白关键词同样被拒");

        Exec(c, ctx, "trade_spread", """{"items":[]}""",
            text => text.Contains("不能为空"), "trade_spread：空 items → 明确原因");
        Exec(c, ctx, "trade_spread", """{"items":["绝不存在的东西XYZ"]}""",
            // 工具先看比价设置里有没有市场（没有就直接提示去配置），有市场时才会走到物品解析
            text => text.Contains("比价设置里没有市场") || text.Contains("没有解析出任何物品"),
            "trade_spread：未配置市场 / 物品不存在都给出明确原因（不发 ESI）");

        Exec(c, ctx, "mission_info", """{}""",
            text => text.Contains("keyword") && text.Contains("level"), "mission_info：既无关键词也无等级 → 明确原因");
        Exec(c, ctx, "mission_info", """{"keyword":"绝不存在任务XYZ"}""",
            text => text.Contains("没有匹配"), "mission_info：未命中 → 明确原因（本地库）");
        Exec(c, ctx, "mission_info", """{"level":6}""",
            text => text.Contains("keyword"), "mission_info：越界等级按「未给等级」处理并提示");

        // ---------- 阶段 116：三个写工具（只验证"挂起语义"，真实写入在 WriteToolSuite 里做并还原） ----------
        foreach (var name in new[] { "glossary_write", "market_favorite", "shopping_cart" })
        {
            var tool = ToolRegistry.Find(name);
            c.That(tool is not null, $"{name}：已注册（阶段 116 写工具）");
            c.That(tool?.RequiresConfirmation == true, $"{name}：写工具必须要求确认");
            c.That(tool?.IsAction == false, $"{name}：写工具不是导航动作");
        }

        // ---------- 阶段 119：阶段 117 的"角色 ESI 直读七个"已删除 ----------
        // 能力改由 ESI 通用网关承担（esi_search → esi_describe → esi_read），名称富化下沉为
        // EsiResponseEnricher（对所有端点生效）。这里断言它们**不会被悄悄加回来**，
        // 免得又出现"同一端点既有专用工具又有网关"的双份维护。
        foreach (var name in new[]
                 {
                     "character_assets", "character_blueprints", "character_standings",
                     "character_contacts", "character_loyalty", "character_calendar", "character_medals",
                 })
        {
            c.That(ToolRegistry.Find(name) is null, $"{name}：已删除（能力由 esi_* 网关承担，不要加回来）");
        }
    }

    /// <summary>执行一个工具并断言结果（含"结果不超过 4000 字符"的公共约束）。</summary>
    private static void Exec(Check c, ToolContext ctx, string toolName, string argsJson, Func<string, bool> expect, string label)
    {
        var result = ExecResult(c, ctx, toolName, argsJson, label);
        if (result is null)
        {
            return;
        }

        c.That(expect(result.Text), label);
        c.That(result.Text.Length <= 4000, $"{label}：结果长度 ≤ 4000 字符（实际 {result.Text.Length}）");
    }

    /// <summary>执行工具并返回完整结果（用于断言 <c>NavigateAction</c> 等非文本字段）。</summary>
    private static ToolResult? ExecResult(Check c, ToolContext ctx, string toolName, string argsJson, string label)
    {
        var tool = ToolRegistry.Find(toolName);
        if (tool is null)
        {
            c.That(false, $"{label}（工具未注册：{toolName}）");
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(argsJson);
            return ProbeApplication.RunBlocking(() =>
                tool.ExecuteAsync(ctx, document.RootElement.Clone(), CancellationToken.None));
        }
        catch (Exception ex)
        {
            c.That(false, $"{label}（抛异常：{ex.GetType().Name} {ex.Message}）");
            return null;
        }
    }
}
