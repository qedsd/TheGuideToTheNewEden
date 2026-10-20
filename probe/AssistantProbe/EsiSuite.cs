using System.Net;
using System.Text.Json;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.Services.Assistant.Esi;

namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// 阶段 118：ESI 通用网关（esi_search / esi_describe / esi_read / esi_write）的离线断言矩阵。
/// <para>
/// 全部断言只读本地索引 + 纯函数，**不发网络请求**（唯一的联网项是最后那段匿名 <c>/status</c> 冒烟，
/// 连不上时明确跳过而不是记失败）。写操作在探针里只验证"会被挂起"与摘要文案——
/// 真正执行写操作需要用户确认，探针绝不代点。
/// </para>
/// </summary>
internal static class EsiSuite
{
    public static void Run(Check c)
    {
        ProbeCore.Ensure();

        var index = RunIndex(c);
        RunSearch(c, index);
        RunFind(c, index);
        RunBuildUrl(c, index);
        RunDenyAndErrors(c, index);
        RunShaper(c);
        RunEnricher(c);
        RunTools(c, index);
        RunLiveSmoke(c);
    }

    // ------------------------------------------------------------------
    //  一、索引完整性
    // ------------------------------------------------------------------
    private static EsiOperation RunIndex(Check c)
    {
        c.Section("EsiSuite · 索引完整性（197 个操作的元数据）");
        c.That(EsiOperationIndex.IsReady, "索引已加载（Resources/Esi/EsiOperations.json 就位）");

        var operations = EsiOperationIndex.All;
        c.Equal(operations.Count, 197, "操作总数");
        c.Equal(EsiOperationIndex.File?.OperationCount ?? -1, operations.Count, "文件里的 operationCount 与实际条数一致");
        c.That(!string.IsNullOrWhiteSpace(EsiOperationIndex.File?.SpecVersion), $"索引记录了规格版本（{EsiOperationIndex.File?.SpecVersion}）");
        c.That(!string.IsNullOrWhiteSpace(EsiOperationIndex.File?.GeneratedAt), $"索引记录了生成时间（{EsiOperationIndex.File?.GeneratedAt}）");

        c.Equal(operations.Select(o => o.Id).Distinct(StringComparer.Ordinal).Count(), operations.Count, "operationId 全局唯一");
        c.That(operations.All(o => !string.IsNullOrWhiteSpace(o.Id)), "operationId 均非空");
        c.That(operations.All(o => o.Path.StartsWith('/')), "路径均以 / 开头");
        c.That(operations.All(o => o.Verb is "GET" or "POST" or "PUT" or "DELETE"), "动词都在预期集合内");

        var writes = operations.Where(o => o.Write).ToList();
        c.Equal(writes.Count, 26, "写操作（需确认）数量");
        c.Equal(operations.Count(o => !o.Write), 171, "只读操作数量");
        c.That(operations.All(o => !(o.Write && o.Verb == "GET")), "没有 GET 被误标成写操作");

        // 查询型 POST（批量查询）绝不能要求确认，否则"查个价格"也要点一次确认
        var readOnlyPosts = EsiOperationIndex.File?.ReadOnlyPosts ?? [];
        c.Equal(readOnlyPosts.Count, 8, "查询型 POST 白名单条数");
        foreach (var path in readOnlyPosts)
        {
            var op = operations.FirstOrDefault(o => string.Equals(o.Path, path, StringComparison.OrdinalIgnoreCase));
            c.That(op is not null && !op.Write, $"查询型 POST 不算写操作：{path}");
        }

        // 参数类型来自参数的 schema 子节点（读错层会让所有参数变成 string）
        c.That(operations.All(o => o.Query.Where(p => p.Name == "page").All(p => p.Type == "integer")),
            "page 参数类型是 integer");
        c.That(operations.All(o => o.Query.Any(p => p.Name == "page") == o.Pageable),
            "Pageable 标记与 page 参数一致");
        c.That(operations.All(o => o.Query.All(p => p.Name is not ("datasource" or "compatibility_date"))),
            "已剔除服务端参数（datasource / compatibility_date）");

        var status = EsiOperationIndex.Find("GetStatus");
        c.NotNull(status, "匿名端点 GetStatus 在索引里");
        c.That(status is { Auth: false, Write: false, Corp: false }, "GetStatus 是匿名只读端点");

        return operations.First(o => o.Id == "GetCharactersCharacterIdWallet");
    }

    // ------------------------------------------------------------------
    //  二、检索与权限过滤
    // ------------------------------------------------------------------
    private static void RunSearch(Check c, EsiOperation wallet)
    {
        c.Section("EsiSuite · 检索与权限过滤");

        var walletScopes = new HashSet<string>(wallet.Scopes, StringComparer.OrdinalIgnoreCase);
        var hits = EsiOperationIndex.Search("character wallet", null, walletScopes, true, 20);
        c.That(hits.Any(o => o.Id == wallet.Id), "按权限检索：命中 GetCharactersCharacterIdWallet");
        c.That(hits.All(o => o.Scopes.All(s => walletScopes.Contains(s))),
            "按权限检索：结果都在授权范围内（没有白跑 403 的端点）");

        var withoutWallet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "esi-skills.read_skills.v1" };
        var filtered = EsiOperationIndex.Search("character wallet", null, withoutWallet, true, 20);
        c.That(filtered.All(o => o.Id != wallet.Id), "缺少 wallet scope 时该端点被过滤掉");

        var unfiltered = EsiOperationIndex.Search("character wallet", null, null, false, 20);
        c.That(unfiltered.Any(o => o.Id == wallet.Id), "权限未知时不按权限过滤（由服务端 403 兜底）");

        var universe = EsiOperationIndex.Search(null, "Universe", null, false, 60);
        c.Equal(universe.Count, 30, "按 tag 检索 Universe 的操作数");
        c.That(universe.All(o => o.Tag == "Universe"), "tag 过滤结果全属该分组（大小写不敏感）");

        var capped = EsiOperationIndex.Search(null, null, null, false, 3);
        c.Equal(capped.Count, 3, "limit 生效（取 3 条）");
        c.Equal(EsiOperationIndex.Search("zzzzzz", null, null, false, 20).Count, 0, "无匹配时返回空");

        var tags = EsiOperationIndex.Tags();
        c.That(tags.Count > 10, $"分组数量合理（{tags.Count} 组）");
        c.That(tags.Any(t => t.Tag == "Universe" && t.Count == 30), "分组统计里 Universe = 30");
        c.That(tags.SequenceEqual(tags.OrderByDescending(t => t.Count)), "分组按操作数降序");

        c.Equal(EsiOperationIndex.MissingScopes(wallet, walletScopes).Count, 0, "已授权时 MissingScopes 为空");
        c.Equal(EsiOperationIndex.MissingScopes(wallet, withoutWallet).Count, wallet.Scopes.Count,
            "未授权时 MissingScopes 列出全部所需 scope");
    }

    // ------------------------------------------------------------------
    //  三、Find 容错
    // ------------------------------------------------------------------
    private static void RunFind(Check c, EsiOperation wallet)
    {
        c.Section("EsiSuite · Find 容错（模型常写错大小写/给路径）");

        c.Equal(EsiOperationIndex.Find("GetCharactersCharacterIdWallet")?.Id, wallet.Id, "精确 operationId");
        c.Equal(EsiOperationIndex.Find("getcharacterscharacteridwallet")?.Id, wallet.Id, "忽略大小写");
        c.Equal(EsiOperationIndex.Find("GET /characters/{character_id}/wallet")?.Id, wallet.Id, "`VERB /path` 形式");
        c.Equal(EsiOperationIndex.Find("/characters/{character_id}/wallet")?.Id, wallet.Id, "纯路径（唯一命中）");
        c.That(EsiOperationIndex.Find("wallet") is null, "歧义片段返回 null（让模型先检索，避免瞎猜端点）");
        c.That(EsiOperationIndex.Find("没有这个操作") is null, "未知名称返回 null");
        c.That(EsiOperationIndex.Find("CorporationIdFacilities") is not null, "唯一片段可以命中");
        c.That(EsiOperationIndex.Find(null) is null && EsiOperationIndex.Find("  ") is null, "空输入返回 null");
    }

    // ------------------------------------------------------------------
    //  四、URL 构造与参数校验
    // ------------------------------------------------------------------
    private static void RunBuildUrl(Check c, EsiOperation wallet)
    {
        c.Section("EsiSuite · URL 构造与参数校验");

        var empty = JsonDocument.Parse("{}").RootElement;

        var built = EsiGateway.BuildUrl(wallet, EsiGateway.TranquilityBase, empty, empty, 12345, 67890, "zh");
        c.That(built.Error is null, "默认参数下构造成功（character_id 取当前角色）");
        c.Equal(built.Url, $"{EsiGateway.TranquilityBase}/characters/12345/wallet", "URL 拼接正确");
        c.Equal(built.Language, "zh", "支持 Accept-Language 的端点带上语言");

        var noLanguage = EsiGateway.BuildUrl(wallet, EsiGateway.TranquilityBase, empty, empty, 12345, 67890, null);
        c.That(noLanguage.Language is null, "未给语言时不下发 Accept-Language");

        var explicitPath = JsonDocument.Parse("""{"character_id": 999}""").RootElement;
        var overridden = EsiGateway.BuildUrl(wallet, EsiGateway.TranquilityBase, explicitPath, empty, 12345, 67890, "en");
        c.Equal(overridden.Url, $"{EsiGateway.TranquilityBase}/characters/999/wallet", "path 里的显式值覆盖默认角色");

        // 缺必填路径参数（type_id）
        var typesOp = EsiOperationIndex.Find("GetUniverseTypesTypeId")!;
        var missingPath = EsiGateway.BuildUrl(typesOp, EsiGateway.TranquilityBase, empty, empty, 0, 0, "en");
        c.That(missingPath.Error is not null && missingPath.Error.Contains("type_id"),
            "缺必填路径参数时给出明确错误（提到 type_id）");

        // 缺必填查询参数（autopilot 有 add_to_beginning / clear_other_waypoints / destination_id 三个必填）
        var autopilot = EsiOperationIndex.Find("PostUiAutopilotWaypoint")!;
        var missingQuery = EsiGateway.BuildUrl(autopilot, EsiGateway.TranquilityBase, empty, empty, 0, 0, "en");
        c.That(missingQuery.Error is not null && missingQuery.Error.Contains("缺少必填查询参数"),
            "缺必填查询参数时给出明确错误");
        c.That(missingQuery.Error is not null && missingQuery.Error.Contains("add_to_beginning"),
            "错误里点名第一个缺的查询参数");

        var autopilotQuery = JsonDocument.Parse(
            """{"destination_id": 30000142, "clear_other_waypoints": false, "add_to_beginning": false}""").RootElement;
        var autopilotOk = EsiGateway.BuildUrl(autopilot, EsiGateway.TranquilityBase, empty, autopilotQuery, 0, 0, "en");
        c.That(autopilotOk.Error is null && autopilotOk.Url.Contains("destination_id=30000142"),
            "必填查询参数齐全时构造成功");

        // 不认识的查询参数：忽略并提示，不进 URL
        var junkQuery = JsonDocument.Parse("""{"page": 2, "nonsense": "x"}""").RootElement;
        var junk = EsiGateway.BuildUrl(wallet, EsiGateway.TranquilityBase, empty, junkQuery, 1, 1, "en");
        c.That(junk.Notes.Any(n => n.Contains("nonsense")), "不认识的查询参数被记入提示");
        c.That(!junk.Url.Contains("nonsense"), "不认识的查询参数不会进 URL");

        // 数组型查询参数 → 逗号连接
        var contacts = EsiOperationIndex.Find("PutCharactersCharacterIdContacts")!;
        var arrayQuery = JsonDocument.Parse("""{"standing": 10, "label_ids": [1, 2, 3]}""").RootElement;
        var arrayBuilt = EsiGateway.BuildUrl(contacts, EsiGateway.TranquilityBase, empty, arrayQuery, 7, 8, "en");
        c.That(arrayBuilt.Error is null && arrayBuilt.Url.Contains("label_ids=1%2C2%2C3"),
            "数组型查询参数按逗号连接并转义");

        // 路径参数转义（中文/空格）
        var regionOp = EsiOperationIndex.Find("GetUniverseSystemsSystemId")!;
        var systemPath = JsonDocument.Parse("""{"system_id": "30000142"}""").RootElement;
        var escaped = EsiGateway.BuildUrl(regionOp, EsiGateway.TranquilityBase, systemPath, empty, 0, 0, "en");
        c.That(escaped.Error is null && escaped.Url.EndsWith("/universe/systems/30000142") && !escaped.Url.Contains('{'),
            "路径参数替换干净（不残留占位符）");

        var chinesePath = JsonDocument.Parse("""{"system_id": "吉 他"}""").RootElement;
        var chinese = EsiGateway.BuildUrl(regionOp, EsiGateway.TranquilityBase, chinesePath, empty, 0, 0, "en");
        c.That(chinese.Url.Contains("%20") && chinese.Url.Contains('%'), "路径参数做 URL 编码（空格/中文）");

        // page 参数（分页端点；region_id 是路径参数，必须显式给——它不能从角色推导）
        var orders = EsiOperationIndex.Find("GetMarketsRegionIdOrders")!;
        var regionPath = JsonDocument.Parse("""{"region_id": 10000002}""").RootElement;
        var pageQuery = JsonDocument.Parse("""{"page": 3, "order_type": "sell"}""").RootElement;
        var paged = EsiGateway.BuildUrl(orders, EsiGateway.TranquilityBase, regionPath, pageQuery, 0, 0, "en");
        c.That(paged.Error is null, "分页端点：显式给 region_id 后构造成功");
        c.That(paged.Url.Contains("page=3") && paged.Url.Contains("order_type=sell"), "分页与过滤参数进入 URL");
        c.That(paged.Url.Contains("/markets/10000002/orders"), "路径参数被正确替换");

        // 缺路径参数（region_id）时明确报错，而不是拼出半截 URL
        var missingRegion = EsiGateway.BuildUrl(orders, EsiGateway.TranquilityBase, empty, pageQuery, 0, 0, "en");
        c.That(missingRegion.Error is not null && missingRegion.Error.Contains("region_id"),
            "缺路径参数 region_id 时明确报错");

        // 富化值回传防御：path/query 里出现 "名称 (id)" 时还原成 ID（否则会拼出必然失败的 URL）
        var enrichedPath = JsonDocument.Parse("""{"region_id": "伏尔戈 (10000002)"}""").RootElement;
        var enrichedQuery = JsonDocument.Parse("""{"order_type":"sell","type_id": "多米尼克斯级 (645)"}""").RootElement;
        var unwrappedUrl = EsiGateway.BuildUrl(orders, EsiGateway.TranquilityBase, enrichedPath, enrichedQuery, 0, 0, "en");
        c.That(unwrappedUrl.Error is null
               && unwrappedUrl.Url.Contains("/markets/10000002/orders")
               && unwrappedUrl.Url.Contains("type_id=645"),
            "path/query 里的富化文本被还原成 ID（不会把名称拼进 URL）");
    }

    // ------------------------------------------------------------------
    //  五、黑名单与错误归一化
    // ------------------------------------------------------------------
    private static void RunDenyAndErrors(Check c, EsiOperation wallet)
    {
        c.Section("EsiSuite · 黑名单与错误归一化");

        var mail = EsiOperationIndex.Find("PostCharactersCharacterIdMail")!;
        c.That(EsiGateway.DenyReason(mail, "/characters/*/mail") is not null, "通配黑名单命中发邮件端点");
        c.That(EsiGateway.DenyReason(wallet, "/characters/*/mail") is null, "黑名单不误伤只读端点");
        c.That(EsiGateway.DenyReason(mail, "PostCharactersCharacterIdMail") is not null, "黑名单支持 operationId");
        c.That(EsiGateway.DenyReason(mail, "") is null && EsiGateway.DenyReason(mail, null) is null,
            "黑名单为空时全部放行");

        var forbidden = EsiGateway.DescribeFailure(HttpStatusCode.Forbidden, "{\"error\":\"x\"}", wallet);
        c.That(forbidden.Contains(wallet.Scopes[0]), "403 文案列出所需 scope");
        c.That(forbidden.Contains("权限选择"), "403 文案给出重新授权的去处");

        var corp = EsiOperationIndex.Find("GetCorporationsCorporationIdMembers")!;
        var corpForbidden = EsiGateway.DescribeFailure(HttpStatusCode.Forbidden, null, corp);
        c.That(corpForbidden.Contains("职位"), "403 文案对军团端点提示需要职位");

        c.That(EsiGateway.DescribeFailure(HttpStatusCode.NotFound, null, wallet).Contains("找不到"),
            "404 → 找不到资源");
        c.That(EsiGateway.DescribeFailure((HttpStatusCode)420, null, wallet).Contains("配额"),
            "420 → 错误配额");
        c.That(EsiGateway.DescribeFailure(HttpStatusCode.Unauthorized, null, wallet).Contains("重新授权"),
            "401 → 提示重新授权");
        c.That(EsiGateway.DescribeFailure(HttpStatusCode.InternalServerError, null, wallet).Contains("服务端"),
            "5xx → 服务端错误");
        c.That(EsiGateway.DescribeFailure(HttpStatusCode.BadRequest, "{\"error\":\"bad param\"}", wallet).Contains("bad param"),
            "失败文案带上服务端返回摘要");
    }

    // ------------------------------------------------------------------
    //  六、整形器
    // ------------------------------------------------------------------
    private static void RunShaper(Check c)
    {
        c.Section("EsiSuite · JSON 整形器");

        var table = EsiJsonShaper.Shape("""[{"name":"吉他","price":5.5},{"name":"皮尔米特","price":6}]""");
        c.That(table.Contains("| name") && table.Contains("| price"), "对象数组 → 表格（含列头）");
        c.That(table.Contains("吉他") && table.Contains("5.5"), "表格含单元格内容");

        var many = "[" + string.Join(",", Enumerable.Range(1, 40).Select(i => $$"""{"id":{{i}}}""")) + "]";
        var capped = EsiJsonShaper.Shape(many);
        c.That(capped.Contains("另有 10 条"), "行数上限 30（40 条时提示另有 10 条）");

        c.Equal(EsiJsonShaper.Shape("[1,2,3]"), "1, 2, 3", "标量数组 → 逗号连接");
        c.Equal(EsiJsonShaper.Shape("[]"), "（空列表）", "空数组有明确文案");

        var obj = EsiJsonShaper.Shape("""{"balance":123.45,"name":"QEDSD"}""");
        c.That(obj.Contains("- balance: 123.45") && obj.Contains("- name: QEDSD"), "对象 → 键值行");

        // 大对象（多个长字段）必须整体截断，而不是只把单个字段截短
        var bulky = "{" + string.Join(",", Enumerable.Range(1, 12)
            .Select(i => $"\"field{i}\":\"{new string((char)('a' + i), 90)}\"")) + "}";
        var longText = EsiJsonShaper.Shape(bulky, 200);
        c.That(longText.Length <= 220, "超长内容按 maxChars 截断");
        c.That(longText.Contains("截断"), "截断后注明已截断");

        c.Equal(EsiJsonShaper.Shape("not json at all"), "not json at all", "非法 JSON 原样返回（不抛异常）");
        c.Equal(EsiJsonShaper.Shape(null), "（空响应）", "空响应有明确文案");
        c.Equal(EsiJsonShaper.CountItems("[1,2,3]"), 3, "CountItems 统计数组长度");
        c.Equal(EsiJsonShaper.CountItems("""{"a":1,"b":2}"""), 2, "CountItems 统计对象字段数");

        var nested = EsiJsonShaper.Shape("""{"outer":{"inner":[{"k":1}]}}""");
        c.That(nested.Contains("outer") && nested.Contains("inner"), "嵌套对象可读展开（不丢层级）");
    }

    // ------------------------------------------------------------------
    //  七、ID → 名称富化（阶段 119：阶段 117 那 7 个工具的富化下沉到这里）
    // ------------------------------------------------------------------
    private static void RunEnricher(Check c)
    {
        c.Section("EsiSuite · ID → 名称富化");

        // 本地 SDE 三类（物品 / 星系 / 星域）：不发网络，名字随本地化设置中英二选一
        var type = Enrich("""{"type_id":645}""");
        c.That(type.Contains("645"), "物品 ID 仍在结果里（格式 名称 (id)）");
        c.That(type.Contains("多米尼克斯") || type.Contains("Dominix", StringComparison.OrdinalIgnoreCase),
            "物品 type_id 645 解析出舰船名（中/英随本地化设置）");

        var system = Enrich("""{"solar_system_id":30000142}""");
        c.That(system.Contains("吉他") || system.Contains("Jita", StringComparison.OrdinalIgnoreCase),
            "星系 solar_system_id 30000142 解析出星系名");

        var region = Enrich("""{"region_id":10000002}""");
        c.That(region.Contains("伏尔戈") || region.Contains("The Forge", StringComparison.OrdinalIgnoreCase),
            "星域 region_id 10000002 解析出星域名");

        // station_id 走本地 SDE（离线确定性）；location_id 是多态字段（空间站/结构/物品容器），
        // 本地认得就补名字、认不得就保持原值——两种情况都合法，所以这里只断言"原 ID 不丢"。
        var station = Enrich("""{"station_id":60003760}""");
        c.That(station.Contains("(60003760)"), "空间站 station_id 解析出站名（本地 SDE，离线确定性）");

        var location = Enrich("""{"location_id":60003760}""");
        c.That(location.Contains("60003760"), "location_id 多态字段至少保留原 ID（解析与否都合法）");

        // 结构 ID（> int.MaxValue）：只查本地结构列表，查不到必须保持原值（不写空、不写错）。
        // 用一个肯定不在本地列表里的 ID——不能用真实市场建筑（用户 MarketStructures.json 里有，会解析成功）。
        var structure = Enrich("""{"location_id":1234567890123}""");
        c.That(structure.Contains("1234567890123") && !structure.Contains("(1234567890123)"),
            "结构 ID 不在本地列表时保持原值（不瞎写名字）");

        var unknown = Enrich("""{"type_id":987654321}""");
        c.That(unknown.Contains("987654321") && !unknown.Contains("("), "未知 ID 保持原值");

        var array = Enrich("""{"type_ids":[645]}""");
        c.That(array.Contains("("), "纯数字数组字段（type_ids）逐元素富化");

        var nested = Enrich("""{"rows":[{"type_id":645,"amount":5}],"count":1}""");
        c.That(nested.Contains("(") && nested.Contains("amount"), "嵌套对象里的 ID 也能富化，且不破坏其它字段");

        var filtered = Enrich("""{"is_blueprint_copy":true,"quantity":3}""");
        c.That(!filtered.Contains("("), "非 ID 字段（布尔/数量）不受影响");

        var invalid = Enrich("not json");
        c.Equal(invalid, "not json", "非 JSON 原样返回");
        c.Equal(Enrich(""), "", "空响应原样返回");

        // 上限：超过 MaxIdsPerResponse 个不同 ID 时截断并标记。
        // 注意必须用**映射表里的字段名**（type_id），否则收集不到候选、根本走不到上限逻辑
        // （第一版写成 "k0":… 这种自造字段名，于是断言恒假——探针自身的坑，记在案）。
        var many = "[" + string.Join(",", Enumerable.Range(0, EsiResponseEnricher.MaxIdsPerResponse + 50)
            .Select(i => $$"""{"type_id":{{900000000 + i}}}""")) + "]";
        var capped = ProbeApplication.RunBlocking(() => EsiResponseEnricher.EnrichAsync(many, default));
        c.That(capped.Truncated, $"ID 数超过上限（{EsiResponseEnricher.MaxIdsPerResponse}）时标记截断");
        c.That(!string.IsNullOrWhiteSpace(capped.Json), "截断时仍返回合法结果（不抛异常）");

        c.Equal(AiAssistantSettingService.DefaultEsiMaxPages, 10, "allPages 默认页数已提到 10（阶段 119）");

        // ---- 富化值回传的防御：模型常把 "名称 (id)" 原样当参数传回来 ----
        c.Equal(EsiResponseEnricher.UnwrapId("吉他 (30000142)"), "30000142", "从富化文本取回 ID");
        c.Equal(EsiResponseEnricher.UnwrapId("伏尔戈(10000002)"), "10000002", "括号紧贴也能取回");
        c.Equal(EsiResponseEnricher.UnwrapId("Foo (Bar) (123)"), "123", "名称里本身带括号时取最后一个 ID");
        c.That(EsiResponseEnricher.UnwrapId("30000142") is null, "纯数字不是富化文本（不还原）");
        c.That(EsiResponseEnricher.UnwrapId("Rifter") is null, "普通名词不还原");

        c.That(EsiResponseEnricher.IsIdField("type_id") && EsiResponseEnricher.IsIdField("region_ids")
               && EsiResponseEnricher.IsIdField("structure_id") && EsiResponseEnricher.IsIdField("whatever_id"),
            "ID 类字段判定：映射表内 + _id/_ids 结尾");
        c.That(!EsiResponseEnricher.IsIdField("subject") && !EsiResponseEnricher.IsIdField("valid"), "非 ID 字段不参与还原");

        // 请求体里的 ID 字段还原成数字，其它字段原样。
        // 注意 `recipients` 本身不是 ID 字段（ESI 邮件体里它是对象数组），真正的 ID 字段是里层的 `recipient_id`；
        // 数组型 ID 字段用 `label_ids`（`_ids` 结尾）来验。
        var unwrapped = EsiResponseEnricher.UnwrapIdsInJson(
            """{"location_id":"吉他 (60003760)","label_ids":["Foo (1)",2],"recipients":[{"recipient_id":"Bar (7)","recipient_type":"character"}],"subject":"你好 (1)"}""");
        c.That(unwrapped.Contains("60003760") && !unwrapped.Contains("吉他 (60003760)"), "请求体里的富化 ID 还原为数字");
        c.That(unwrapped.Contains("[1,2]"), "请求体数组（label_ids）里的富化 ID 也还原");
        c.That(unwrapped.Contains("\"Bar (7)\"") == false, "请求体嵌套对象里的 ID 字段（recipient_id）也还原");
        c.That(unwrapped.Contains("你好 (1)"), "请求体里**非 ID 字段**的括号数字不被误改");
        c.Equal(EsiResponseEnricher.UnwrapIdsInJson("not json"), "not json", "非 JSON 请求体原样返回");
    }

    private static string Enrich(string json)
        => ProbeApplication.RunBlocking(() => EsiResponseEnricher.EnrichAsync(json, default)).Json;

    // ------------------------------------------------------------------
    //  八、工具面（离线）
    // ------------------------------------------------------------------
    private static void RunTools(Check c, EsiOperation wallet)
    {
        c.Section("EsiSuite · 工具面（注册 / Schema / 分工 / 摘要）");

        var names = new[] { "esi_search", "esi_describe", "esi_read", "esi_write" };
        c.That(ToolRegistry.All.Count(t => !t.Name.StartsWith("probe_", StringComparison.Ordinal)) >= 46,
            "产品工具数不少于 46（阶段 119 之后的基线；后续新增的配置/功能重启工具会继续增加）");
        foreach (var name in names)
        {
            var tool = ToolRegistry.Find(name);
            c.NotNull(tool, $"工具已注册：{name}");
            if (tool is null)
            {
                continue;
            }

            c.Equal(tool.DisplayNameKey, $"AssistantTool_{name}", $"{name} 的本地化键符合约定");
            c.That(tool.Parameters is { Count: > 0 }, $"{name} 有参数 schema");
            if (tool.Parameters is { } schema)
            {
                var props = schema["properties"] as System.Text.Json.Nodes.JsonObject;
                var required = schema["required"] as System.Text.Json.Nodes.JsonArray;
                c.That(props is { Count: > 0 }, $"{name} 的 schema 有 properties");
                c.That(required is null || required.All(r => props!.ContainsKey(r!.GetValue<string>())),
                    $"{name} 的 required 都是已声明字段");
            }
        }

        var read = ToolRegistry.Find("esi_read")!;
        var write = ToolRegistry.Find("esi_write")!;
        c.That(!read.RequiresConfirmation, "esi_read 不需要确认");
        c.That(write.RequiresConfirmation, "esi_write 需要确认（写操作必须挂起）");
        c.That(write is IPendingSummary, "esi_write 实现了待确认动作的人话摘要");

        var ctx = new ToolContext
        {
            Character = null,
            Characters = Array.Empty<AuthorizedCharacterData>(),
            OnUiThread = _ => { },
        };

        // 分工：只读工具拒绝写端点；写工具拒绝只读端点（都在发请求之前就返回）
        var mailVerdict = read.ExecuteAsync(ctx, Args("""{"operation":"PostCharactersCharacterIdMail"}"""), default)
            .GetAwaiter().GetResult();
        c.That(mailVerdict.Text.Contains("esi_write"), "esi_read 拒绝写端点并指向 esi_write");

        var walletVerdict = write.ExecuteAsync(ctx, Args("""{"operation":"GetCharactersCharacterIdWallet"}"""), default)
            .GetAwaiter().GetResult();
        c.That(walletVerdict.Text.Contains("esi_read"), "esi_write 拒绝只读端点并指向 esi_read");

        var unknownVerdict = read.ExecuteAsync(ctx, Args("""{"operation":"GetNoSuchThing"}"""), default)
            .GetAwaiter().GetResult();
        c.That(unknownVerdict.Text.Contains("esi_search"), "未知 operationId 时引导先检索");

        var missingVerdict = read.ExecuteAsync(ctx, Args("{}"), default).GetAwaiter().GetResult();
        c.That(missingVerdict.Text.Contains("operation"), "缺 operation 参数时给出明确提示");

        // 检索 / 说明
        var searchNoKeyword = ToolRegistry.Find("esi_search")!
            .ExecuteAsync(ctx, Args("{}"), default).GetAwaiter().GetResult();
        c.That(searchNoKeyword.Text.Contains("分组") && searchNoKeyword.Text.Contains("Universe"),
            "esi_search 无关键词时列出分组");

        var searchHit = ToolRegistry.Find("esi_search")!
            .ExecuteAsync(ctx, Args("""{"keyword":"wallet","limit":5}"""), default).GetAwaiter().GetResult();
        c.That(searchHit.Text.Contains("GetCharactersCharacterIdWallet"), "esi_search 关键词命中钱包端点");

        var described = ToolRegistry.Find("esi_describe")!
            .ExecuteAsync(ctx, Args("""{"operation":"PostCharactersCharacterIdMail"}"""), default).GetAwaiter().GetResult();
        c.That(described.Text.Contains("POST /characters/{character_id}/mail"), "esi_describe 给出动词与路径");
        c.That(described.Text.Contains("recipients") && described.Text.Contains("subject") && described.Text.Contains("body"),
            "esi_describe 列出请求体必填字段");
        c.That(described.Text.Contains("esi-mail.send_mail.v1"), "esi_describe 列出所需 scope");
        c.That(described.Text.Contains("esi_write"), "esi_describe 给出写工具的调用示例");

        var describeUnknown = ToolRegistry.Find("esi_describe")!
            .ExecuteAsync(ctx, Args("""{"operation":"NoSuchOp"}"""), default).GetAwaiter().GetResult();
        c.That(describeUnknown.Text.Contains("esi_search"), "esi_describe 未知操作时引导检索");

        // 待确认摘要（确认卡片上显示的就是它）
        var pending = ((IPendingSummary)write).DescribePending(Args(
            """{"operation":"PostCharactersCharacterIdMail","path":{"character_id":211},"body":{"recipients":[1,2],"subject":"你好","body":"正文"}}"""));
        c.That(pending.Contains("POST /characters/{character_id}/mail"), "确认摘要含动词与路径");
        c.That(pending.Contains("character_id=211"), "确认摘要含路径参数");
        c.That(pending.Contains("subject") && pending.Contains("你好"), "确认摘要含请求体关键字段");
        c.That(pending.Contains("Send a new mail"), "确认摘要带上端点说明（人话）");

        var pendingUnknown = ((IPendingSummary)write).DescribePending(Args("""{"operation":"NoSuchOp"}"""));
        c.That(pendingUnknown.Contains("未知"), "未知操作的确认摘要不抛异常");

        c.That(EsiGateway.Describe(wallet).Contains("无需授权") == false, "Describe 对有权限的端点不写'无需授权'");
        c.Equal(EsiGateway.Describe(wallet).Contains("esi_read"), true, "Describe 给出只读工具示例");
    }

    // ------------------------------------------------------------------
    //  九、联网冒烟（匿名端点；连不上只跳过，不算失败）
    // ------------------------------------------------------------------
    private static void RunLiveSmoke(Check c)
    {
        c.Section("EsiSuite · 联网冒烟（匿名 GET /status，连不上则跳过）");

        var status = EsiOperationIndex.Find("GetStatus");
        if (status is null)
        {
            c.That(false, "索引里应有 GetStatus");
            return;
        }

        var ctx = new ToolContext
        {
            Character = null,
            Characters = Array.Empty<AuthorizedCharacterData>(),
            OnUiThread = _ => { },
        };

        string text;
        try
        {
            text = ProbeApplication.RunBlocking(() => EsiGateway.InvokeAsync(status, ctx, Args("{}"), false, default));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  SKIP  联网冒烟跳过（{ex.GetType().Name}: {ex.Message}）");
            return;
        }

        if (text.Contains("请求失败") || text.Contains("超时"))
        {
            Console.WriteLine($"  SKIP  联网冒烟跳过（{text.Split('\n')[0]}）");
            return;
        }

        c.That(text.Contains("GET /status → 200"), "匿名调用 /status 返回 200");
        c.That(text.Contains("players") || text.Contains("server_version"), "响应整形后含已知字段（players / server_version）");
    }

    private static JsonElement Args(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
