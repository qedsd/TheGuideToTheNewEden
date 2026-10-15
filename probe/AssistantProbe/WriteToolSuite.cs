using System.IO;
using System.Text;
using System.Text.Json;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.Services.Business;
using TheGuideToTheNewEden.WPF.Services.Translation;

namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// 阶段 116 的三个**写工具**真实执行并还原：
/// <c>glossary_write</c>（用户术语表）、<c>market_favorite</c>（市场收藏）、<c>shopping_cart</c>（倒货购物车）。
/// <list type="bullet">
///   <item>为什么单独一套：这三个工具 <c>RequiresConfirmation=true</c>，引擎正常路径下**不会**执行它们
///         （由用户点确认卡片后才跑），因此 ToolSuite 只能验到"挂起标记"；真正的写盘/写集合行为必须直连工具执行；</item>
///   <item><b>会改用户数据</b>：术语表与市场收藏都是真实落盘的文件，本套件在 <c>finally</c> 里按字节还原；
///         购物车是内存集合（不落盘），测试后清掉自己加的行。<b>请在应用未运行时跑</b>。</item>
/// </list>
/// </summary>
internal static class WriteToolSuite
{
    private static string GlossaryFile => Path.Combine(SettingsService.DataPath, "Configs", "UserGlossary.json");

    private static string StaredFile => Path.Combine(SettingsService.DataPath, "Configs", "StaredMarketInvType.json");

    /// <summary>测试用术语（故意用不可能与真实数据冲突的名字）。</summary>
    private const string TestEnglish = "ProbeWriteToolSuite Fake Hull";

    private const string TestChinese = "探针假船体";

    public static void Run(Check c)
    {
        ProbeCore.Ensure();

        c.Section("WriteToolSuite · 环境与快照");
        c.That(!string.IsNullOrWhiteSpace(SettingsService.DataPath), $"设置目录就绪（{SettingsService.DataPath}）");

        // 本套件的"落盘"断言要求进程能写 %LocalAppData%。探针 exe 位于工作区内 → 继承工作区的 Low 完整性标签，
        // 而 %LocalAppData% 是 Medium：写入会被直接拒绝（项目踩坑 §8 第 62 条），而各服务的 Save() 是
        // "只记日志不抛异常"的（设计如此），表现为"内存改了、文件没变"。先探一次，再决定要不要断落盘。
        var canWriteConfigs = CanWriteConfigs(out var writeReason);
        c.That(true, $"进程可写配置文件 = {canWriteConfigs}" +
            (canWriteConfigs ? string.Empty : $"（跳过落盘断言：{writeReason}；把探针输出复制到工作区外再跑即可完整验证）"));

        var glossaryBackup = File.Exists(GlossaryFile) ? File.ReadAllBytes(GlossaryFile) : null;
        var staredBackup = File.Exists(StaredFile) ? File.ReadAllBytes(StaredFile) : null;
        var cartSnapshot = BusinessService.Current.ShoppingCart.ToList();
        c.That(true, $"快照：术语表 {(glossaryBackup is null ? "不存在" : $"{glossaryBackup.Length} 字节")}、" +
            $"市场收藏 {(staredBackup is null ? "不存在" : $"{staredBackup.Length} 字节")}、购物车 {cartSnapshot.Count} 行");

        try
        {
            c.Section("WriteToolSuite · 用户术语表（真实写盘）");
            Glossary(c, glossaryBackup, canWriteConfigs);

            c.Section("WriteToolSuite · 市场收藏（真实写盘）");
            Favorite(c, staredBackup, canWriteConfigs);

            c.Section("WriteToolSuite · 倒货购物车（共享内存集合）");
            ShoppingCart(c);
        }
        finally
        {
            Restore(GlossaryFile, glossaryBackup);
            Restore(StaredFile, staredBackup);

            // 购物车是内存集合：只移除本套件加进去的行，用户原有的行原样保留
            foreach (var item in BusinessService.Current.ShoppingCart.ToList())
            {
                if (!cartSnapshot.Contains(item))
                {
                    BusinessService.Current.ShoppingCart.Remove(item);
                }
            }

            c.That(true, $"还原完成：术语表 {Describe(GlossaryFile, glossaryBackup)}、市场收藏 {Describe(StaredFile, staredBackup)}、" +
                $"购物车回到 {BusinessService.Current.ShoppingCart.Count} 行");
        }
    }

    // ============================ 术语表 ============================

    private static void Glossary(Check c, byte[]? backup, bool canWriteConfigs)
    {
        var ctx = Context();
        var beforeCount = UserGlossaryService.Count;

        // 新增
        var add = Exec(c, ctx, "glossary_write",
            $$"""{"action":"add","english":{{Quoted(TestEnglish)}},"chinese":{{Quoted(TestChinese)}},"note":"probe"}""",
            "glossary_write：新增一条术语");
        c.That(add?.Contains("已新增", StringComparison.Ordinal) == true, $"新增回执明确（「{Short(add)}」）");

        var term = UserGlossaryService.FindByEnglish(TestEnglish);
        c.That(term?.Chinese == TestChinese, "术语表里确实写入了该术语（且中文名一致）");
        c.Equal(UserGlossaryService.Count, beforeCount + 1, "术语条数 +1");
        if (canWriteConfigs)
        {
            FileContains(c, GlossaryFile, TestChinese, "术语已落盘到 UserGlossary.json");
        }
        c.That(UserGlossaryService.Signature != "u0", "术语签名随内容变化（AI 结果缓存会失效）");

        // 覆盖（同名英文）
        var update = Exec(c, ctx, "glossary_write",
            $$"""{"action":"add","english":{{Quoted(TestEnglish)}},"chinese":"探针假船体 II"}""",
            "glossary_write：同名英文再写一次 = 覆盖");
        c.That(update?.Contains("已更新", StringComparison.Ordinal) == true, "覆盖回执与新增可区分");
        c.Equal(UserGlossaryService.Count, beforeCount + 1, "覆盖不增加条数");
        c.Equal(UserGlossaryService.FindByEnglish(TestEnglish)?.Chinese, "探针假船体 II", "译名已更新");

        // 缺参
        var noChinese = Exec(c, ctx, "glossary_write", $$"""{"action":"add","english":"ProbeOnlyEnglish"}""",
            "glossary_write：新增缺中文名 → 明确原因");
        c.That(noChinese?.Contains("chinese", StringComparison.Ordinal) == true, "缺中文名时指出缺哪个参数");
        c.That(UserGlossaryService.FindByEnglish("ProbeOnlyEnglish") is null, "缺参时不会写入半条术语");

        var noEnglish = Exec(c, ctx, "glossary_write", """{"chinese":"只有中文"}""",
            "glossary_write：缺英文名 → 明确原因");
        c.That(noEnglish?.Contains("english", StringComparison.Ordinal) == true, "缺英文名时指出缺哪个参数");

        // 删除
        var remove = Exec(c, ctx, "glossary_write",
            $$"""{"action":"remove","english":{{Quoted(TestEnglish)}}}""",
            "glossary_write：按英文名删除");
        c.That(remove?.Contains("已从用户术语表删除", StringComparison.Ordinal) == true, "删除回执明确");
        c.That(UserGlossaryService.FindByEnglish(TestEnglish) is null, "术语已从内存索引移除");
        c.Equal(UserGlossaryService.Count, beforeCount, "术语条数回到测试前");

        var removeAgain = Exec(c, ctx, "glossary_write",
            $$"""{"action":"remove","english":{{Quoted(TestEnglish)}}}""",
            "glossary_write：重复删除不报错、给明确原因");
        c.That(removeAgain?.Contains("没有英文名", StringComparison.Ordinal) == true, "重复删除说明本来就没有");

        if (backup is null)
        {
            // 原本没有这个文件：测试自己建的，直接删掉避免留下空壳（内容差异不影响用户）
            TryDelete(GlossaryFile);
        }
    }

    // ============================ 市场收藏 ============================

    private static void Favorite(Check c, byte[]? backup, bool canWriteConfigs)
    {
        var ctx = Context();
        var service = MarketStarService.Current;
        var jita = ToolNameResolver.ResolveSystem("吉他");
        c.That(jita is not null, "本地库可用（解析出吉他）");
        if (jita is null)
        {
            return;
        }

        // 用一个"肯定不在收藏里"的物品：先移除，再新增，再移除
        const string itemName = "Rifter";
        var invType = ToolNameResolver.ResolveItem(itemName);
        c.That(invType is not null, $"{itemName} 能在本地库解析出物品");
        if (invType is null)
        {
            return;
        }

        var wasStared = service.IsStared(invType.TypeID);
        if (wasStared)
        {
            service.Remove(invType.TypeID);
            c.That(true, $"{itemName} 原本就在收藏里，为测试先移出（finally 会按字节还原文件）");
        }

        var removeMissing = Exec(c, ctx, "market_favorite",
            $$"""{"action":"remove","itemName":{{Quoted(itemName)}}}""",
            "market_favorite：移除不在收藏里的物品 → 明确原因");
        c.That(removeMissing?.Contains("本来就不在收藏里", StringComparison.Ordinal) == true, "未收藏时给出明确原因而不是假装成功");

        var add = Exec(c, ctx, "market_favorite",
            $$"""{"itemName":{{Quoted(itemName)}}}""",
            "market_favorite：收藏物品（action 缺省 = add）");
        c.That(add?.Contains("已收藏", StringComparison.Ordinal) == true, $"收藏回执明确（「{Short(add)}」）");
        c.That(service.IsStared(invType.TypeID), "服务内存里确实标记为已收藏");
        if (canWriteConfigs)
        {
            FileContains(c, StaredFile, invType.TypeID.ToString(), "收藏已落盘到 StaredMarketInvType.json");
        }

        var addAgain = Exec(c, ctx, "market_favorite",
            $$"""{"itemName":{{Quoted(itemName)}}}""",
            "market_favorite：重复收藏不报错、给明确原因");
        c.That(addAgain?.Contains("已经在收藏里", StringComparison.Ordinal) == true, "重复收藏给出明确回执");

        var remove = Exec(c, ctx, "market_favorite",
            $$"""{"action":"remove","itemName":{{Quoted(itemName)}}}""",
            "market_favorite：取消收藏");
        c.That(remove?.Contains("已取消收藏", StringComparison.Ordinal) == true, "取消收藏回执明确");
        c.That(!service.IsStared(invType.TypeID), "服务内存里已移除");

        var unknown = Exec(c, ctx, "market_favorite", """{"itemName":"绝不存在的东西XYZ"}""",
            "market_favorite：物品名解析失败 → 明确原因（不写盘）");
        c.That(unknown?.Contains("未找到物品", StringComparison.Ordinal) == true, "未找到物品时说明原因");

        var noName = Exec(c, ctx, "market_favorite", """{}""",
            "market_favorite：缺 itemName → 明确原因");
        c.That(noName?.Contains("不能为空", StringComparison.Ordinal) == true, "缺参给出原因");

        if (backup is null)
        {
            TryDelete(StaredFile);
        }
    }

    // ============================ 购物车 ============================

    private static void ShoppingCart(Check c)
    {
        var ctx = Context();
        var cart = BusinessService.Current.ShoppingCart;
        var beforeCount = cart.Count;

        const string itemName = "Rifter";
        var invType = ToolNameResolver.ResolveItem(itemName);
        c.That(invType is not null, $"购物车测试物品可解析（{itemName}）");
        if (invType is null)
        {
            return;
        }

        // 同一个物品两条不同物品，方便验证"按物品名移除"与"按行号移除"
        var existing = cart.FirstOrDefault(p => p.InvType?.TypeID == invType.TypeID);
        var baselineQuantity = existing?.Quantity ?? 0;

        var add = Exec(c, ctx, "shopping_cart",
            $$"""{"itemName":{{Quoted(itemName)}},"quantity":100,"buyPrice":1000,"sellPrice":1100,"jumps":5}""",
            "shopping_cart：加入购物车");
        c.That(add?.Contains("已加入", StringComparison.Ordinal) == true, $"加入回执明确（「{Short(add)}」）");
        c.Equal(cart.Count, existing is null ? beforeCount + 1 : beforeCount, "首次加入只增加一种物品");

        var item = cart.First(p => p.InvType?.TypeID == invType.TypeID);
        c.Equal(item.Quantity, baselineQuantity + 100, "数量按本次给的值写入");
        c.Equal(item.BuyPrice, 1000d, "买入价写入");
        c.Equal(item.SellPrice, 1100d, "卖出价写入");
        c.Equal(item.Jumps, 5, "跳数写入");
        c.Between(item.ROI, 9.9, 10.1, "回报率由购物车自己算（(1100-1000)/1000 = 10%）");
        c.Between(item.NetProfit, 9999, 10001, "净利润 = 价差 × 数量 = 10,000");

        var addAgain = Exec(c, ctx, "shopping_cart",
            $$"""{"itemName":{{Quoted(itemName)}},"quantity":50,"buyPrice":1000,"sellPrice":1100}""",
            "shopping_cart：同一物品再加 = 数量累加");
        c.That(addAgain?.Contains("已累加", StringComparison.Ordinal) == true, "重复加入走「累加」分支而不是新增行");
        c.Equal(cart.First(p => p.InvType?.TypeID == invType.TypeID).Quantity, baselineQuantity + 150, "数量累加到 150");

        var noQuantity = Exec(c, ctx, "shopping_cart", $$"""{"itemName":{{Quoted(itemName)}}}""",
            "shopping_cart：缺 quantity → 明确原因");
        c.That(noQuantity?.Contains("quantity", StringComparison.Ordinal) == true, "缺数量时指出缺哪个参数");

        var zeroQuantity = Exec(c, ctx, "shopping_cart", $$"""{"itemName":{{Quoted(itemName)}},"quantity":0}""",
            "shopping_cart：数量 0 → 明确原因");
        c.That(zeroQuantity?.Contains("大于 0", StringComparison.Ordinal) == true, "数量必须为正");

        var unknown = Exec(c, ctx, "shopping_cart", """{"itemName":"绝不存在的东西XYZ","quantity":1}""",
            "shopping_cart：物品名解析失败 → 明确原因（不入车）");
        c.That(unknown?.Contains("未找到物品", StringComparison.Ordinal) == true, "未找到物品时说明原因");

        var badIndex = Exec(c, ctx, "shopping_cart", """{"action":"remove","index":9999}""",
            "shopping_cart：越界行号 → 明确原因");
        c.That(badIndex?.Contains("超出范围", StringComparison.Ordinal) == true, "越界行号给出范围提示");

        var removeNoArg = Exec(c, ctx, "shopping_cart", """{"action":"remove"}""",
            "shopping_cart：移除既无 itemName 也无 index → 明确原因");
        c.That(removeNoArg?.Contains("需要 itemName", StringComparison.Ordinal) == true, "移除缺参给出原因");

        var removeByName = Exec(c, ctx, "shopping_cart",
            $$"""{"action":"remove","itemName":{{Quoted(itemName)}}}""",
            "shopping_cart：按物品名移除");
        c.That(removeByName?.Contains("已从购物车移除", StringComparison.Ordinal) == true, "按名移除回执明确");
        c.That(!cart.Any(p => p.InvType?.TypeID == invType.TypeID), "该物品已离开购物车");

        var removeByIndex = Exec(c, ctx, "shopping_cart", """{"action":"remove","index":1}""",
            "shopping_cart：按行号移除（若购物车为空则越界，属预期）");
        c.That(removeByIndex is not null, "按行号移除有回执（不抛异常）");

        // 收尾：移除本套件新增的行（还原用户原有内容）
        foreach (var extra in cart.ToList())
        {
            if (extra.InvType?.TypeID == invType.TypeID)
            {
                cart.Remove(extra);
            }
        }

        c.Equal(cart.Count, beforeCount, "购物车行数回到测试前");
    }

    // ============================ 基础设施 ============================

    private static ToolContext Context() => new()
    {
        Character = null,
        Characters = [],
        OnUiThread = action => action(),
        Progress = _ => { },
    };

    private static string? Exec(Check c, ToolContext ctx, string toolName, string argsJson, string label)
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
            var result = ProbeApplication.RunBlocking(() =>
                tool.ExecuteAsync(ctx, document.RootElement.Clone(), CancellationToken.None));
            c.That(!string.IsNullOrWhiteSpace(result.Text), label);
            return result.Text + " || " + (result.ParamSummary ?? string.Empty);
        }
        catch (Exception ex)
        {
            c.That(false, $"{label}（抛异常：{ex.GetType().Name} {ex.Message}）");
            return null;
        }
    }

    private static string Quoted(string value) => JsonSerializer.Serialize(value);

    /// <summary>
    /// 探一次"本进程能不能写 %LocalAppData% 里的配置文件"。
    /// 探针 exe 在工作区内 → 继承 Low 完整性标签 → 写 Medium 的 %LocalAppData% 会被拒（§8 第 62 条）。
    /// </summary>
    private static bool CanWriteConfigs(out string reason)
    {
        var canary = Path.Combine(SettingsService.DataPath, "Configs", "WriteProbeSuite.canary.txt");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(canary)!);
            File.WriteAllText(canary, "canary");
            File.Delete(canary);
            reason = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            reason = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
        finally
        {
            TryDelete(canary);
        }
    }

    /// <summary>断言"文件里出现了这段内容"，并把实际状态写进消息（诊断落盘失败时能一眼看清原因）。</summary>
    private static void FileContains(Check c, string path, string expected, string label)
    {
        try
        {
            if (!File.Exists(path))
            {
                // 顺带看一眼同目录，确认不是"写到了别的地方"
                var folder = Path.GetDirectoryName(path)!;
                var siblings = Directory.Exists(folder)
                    ? string.Join("、", Directory.GetFiles(folder).Select(Path.GetFileName))
                    : "(目录不存在)";
                c.That(false, $"{label}（文件不存在：{path}；同目录：{siblings}）");
                return;
            }

            var text = File.ReadAllText(path);
            c.That(text.Contains(expected, StringComparison.Ordinal),
                $"{label}（{text.Length} 字符，{(text.Contains(expected, StringComparison.Ordinal) ? "命中" : "未命中")}「{expected}」）");
        }
        catch (Exception ex)
        {
            c.That(false, $"{label}（读取抛异常：{ex.GetType().Name} {ex.Message}）");
        }
    }

    private static void Restore(string path, byte[]? backup)
    {
        try
        {
            if (backup is null)
            {
                TryDelete(path);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, backup);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private static string Describe(string path, byte[]? backup)
        => backup is null ? "已删除（原本不存在）" : $"已还原 {backup.Length} 字节";

    private static string Short(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "(空)";
        }

        var single = text.Replace("\r", " ").Replace("\n", " ");
        return single.Length <= 60 ? single : single[..57] + "…";
    }
}
