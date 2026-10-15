using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.Services.Map;
using TheGuideToTheNewEden.WPF.ViewModels.Map;
using TheGuideToTheNewEden.WPF.Views.Pages;
using TheGuideToTheNewEden.WPF.Views.UserControls.Map;

namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// 星图命令通道与跳桥写工具（阶段 107/113 补测）。
/// <list type="bullet">
///   <item><b>命令通道</b>：星图页未装载 → 命令回存 pending；已装载 → 即时消费并作用到 VM（图层/跳桥层）；</item>
///   <item><b>写工具</b>：<c>map_jumpbridge_add/remove/import</c> 真实写盘（新增/重复/删除/重删/导入/冲突/未识别/空/坏路径），
///         并在 <c>finally</c> 里把配置**还原**成运行前的样子。</item>
/// </list>
/// <para>
/// 注意：<c>ShowBridges</c> 会经 <c>JumpBridgeSettingService.SetShowBridge</c> 落盘，所以本套件一定先备份
/// <c>Configs/JumpBridgeSetting.json</c> 字节，最后写回，避免污染用户配置。**请在应用未运行时跑**。
/// </para>
/// </summary>
internal static class MapProbe
{
    private static string ConfigFile => Path.Combine(SettingsService.DataPath, "Configs", "JumpBridgeSetting.json");

    public static void Run(Check c)
    {
        ProbeCore.Ensure();
        ProbeApplication.Ensure();

        c.Section("MapProbe · 环境");
        c.That(!string.IsNullOrWhiteSpace(SettingsService.DataPath), $"设置目录就绪（{SettingsService.DataPath}）");

        var backup = File.Exists(ConfigFile) ? File.ReadAllBytes(ConfigFile) : null;
        var initialShow = JumpBridgeSettingService.IsShowBridge();
        var initialBridges = JumpBridgeSettingService.GetBridgesDict();
        c.That(true, $"跳桥配置快照：{initialBridges.Count} 条跳桥，跳桥层显示 = {initialShow}，文件{(backup is null ? "不存在" : $"已备份 {backup.Length} 字节")}");

        try
        {
            c.Section("MapProbe · 星图命令通道（星图页未装载）");
            CommandChannel(c);

            c.Section("MapProbe · 跳桥写工具（真实写盘）");
            JumpBridgeWrites(c);

            c.Section("MapProbe · 星图页真实应用命令");
            PageAppliesCommand(c);
        }
        finally
        {
            // 还原调度上下文：后面的套件里仍有"同步等异步"的调用，留着上下文会让它们的续体
            // 投给没有在泵的消息队列而卡住（探针只在星图页那一步需要它）。
            SynchronizationContext.SetSynchronizationContext(null);

            // 还原：先还原"跳桥层显示"（它会落盘），再把整份配置写回快照
            JumpBridgeSettingService.SetShowBridge(initialShow);
            if (backup is not null)
            {
                File.WriteAllBytes(ConfigFile, backup);
            }

            var after = JumpBridgeSettingService.GetBridgesDict();
            c.That(SameBridges(after, initialBridges),
                $"跳桥配置已还原为运行前状态（{initialBridges.Count} 条 → {after.Count} 条）");
        }
    }

    // ============================ 命令通道 ============================

    private static void CommandChannel(Check c)
    {
        Drain();
        var jita = ToolNameResolver.ResolveSystem("吉他");
        var perimeter = ToolNameResolver.ResolveSystem("皮尔米特");
        if (jita is null || perimeter is null)
        {
            c.That(false, "本地库解析不到「吉他」/「皮尔米特」——SDE 未就绪，命令通道断言无法进行");
            return;
        }

        var command = new Navigation.MapViewCommand
        {
            Route = [jita.SolarSystemID, perimeter.SolarSystemID],
            Waypoints = [1],
            CenterSystemId = jita.SolarSystemID,
            ColorMode = MapColorMode.Kills,
            ShowBridges = true,
        };

        Navigation.NavigateToMap(command);

        var taken = Navigation.TakePendingMapCommand();
        c.That(taken is not null, "星图页未装载时，命令被回存为待应用（pending）");
        c.Equal(taken?.Route?.Count ?? 0, 2, "pending 命令保留了完整路径");
        c.Equal(taken?.Route?[0], jita.SolarSystemID, "路径首端 = 起点星系");
        c.Equal(taken?.Waypoints?.Count ?? 0, 1, "途经点索引一并保留");
        c.Equal(taken?.ColorMode, MapColorMode.Kills, "图层配色一并保留");
        c.Equal(taken?.ShowBridges, true, "跳桥层开关一并保留");
        c.That(Navigation.TakePendingMapCommand() is null, "pending 命令只能被消费一次");

        // 只给 CenterSystemId 的命令（定位型）也必须能回存
        Drain();
        Navigation.NavigateToMap(new Navigation.MapViewCommand { CenterSystemId = jita.SolarSystemID });
        var locate = Navigation.TakePendingMapCommand();
        c.Equal(locate?.CenterSystemId, jita.SolarSystemID, "定位型命令（只给 CenterSystemId）同样回存");
        c.That(locate?.Route is null, "定位型命令不带路径");
    }

    // ============================ 跳桥写工具 ============================

    private static void JumpBridgeWrites(Check c)
    {
        var jita = ToolNameResolver.ResolveSystem("吉他");
        var perimeter = ToolNameResolver.ResolveSystem("皮尔米特");
        if (jita is null || perimeter is null)
        {
            c.That(false, "星系解析失败，跳桥写工具断言无法进行");
            return;
        }

        var idA = jita.SolarSystemID;
        var idB = perimeter.SolarSystemID;
        var before = JumpBridgeSettingService.GetBridgesDict();
        if (before.ContainsKey(idA) || before.ContainsKey(idB))
        {
            c.That(true, $"「{jita.SolarSystemName}」或「{perimeter.SolarSystemName}」已有跳桥 → 跳过写测试（不动用户数据）");
            return;
        }

        var ctx = NewContext();
        var add = Exec(c, ctx, "map_jumpbridge_add", """{"systemA":"吉他","systemB":"皮尔米特"}""", "map_jumpbridge_add：新增一组跳桥");
        c.That(add?.Contains("已添加跳桥", StringComparison.Ordinal) == true, $"新增回执正确（实际「{Short(add)}」）");
        var dict = JumpBridgeSettingService.GetBridgesDict();
        c.That(dict.ContainsKey(idA) && dict[idA] == idB || (dict.ContainsKey(idB) && dict[idB] == idA), "配置里真的写入了这组跳桥");

        var duplicate = Exec(c, ctx, "map_jumpbridge_add", """{"systemA":"吉他","systemB":"皮尔米特"}""", "map_jumpbridge_add：端被占用时不重复添加");
        c.That(duplicate?.Contains("未添加", StringComparison.Ordinal) == true, $"重复添加被拒（实际「{Short(duplicate)}」）");

        var missing = Exec(c, ctx, "map_jumpbridge_add", """{"systemA":"吉他"}""", "map_jumpbridge_add：缺参数给出原因");
        c.That(missing?.Contains("不能为空", StringComparison.Ordinal) == true, "缺 systemB 时明确报参数错误");

        var remove = Exec(c, ctx, "map_jumpbridge_remove", """{"systemA":"吉他","systemB":"皮尔米特"}""", "map_jumpbridge_remove：删除跳桥");
        c.That(remove?.Contains("已删除跳桥", StringComparison.Ordinal) == true, $"删除回执正确（实际「{Short(remove)}」）");
        c.That(!JumpBridgeSettingService.GetBridgesDict().ContainsKey(idA), "配置里该跳桥已移除");

        var removeAgain = Exec(c, ctx, "map_jumpbridge_remove", """{"systemA":"吉他","systemB":"皮尔米特"}""", "map_jumpbridge_remove：重复删除给出原因");
        c.That(removeAgain?.Contains("未找到跳桥", StringComparison.Ordinal) == true, "重复删除时不报错、给明确回执");

        // 导入（文本形式）
        var import = Exec(c, ctx, "map_jumpbridge_import", """{"text":"吉他 ↔ 皮尔米特"}""", "map_jumpbridge_import：文本导入一组");
        c.That(import?.Contains("新增 1 组", StringComparison.Ordinal) == true, $"文本导入新增 1 组（实际「{Short(import)}」）");
        c.That(JumpBridgeSettingService.GetBridgesDict().ContainsKey(idA), "导入后配置里确实有这组跳桥");

        var importConflict = Exec(c, ctx, "map_jumpbridge_import", """{"text":"吉他 ↔ 皮尔米特"}""", "map_jumpbridge_import：端已占用算冲突");
        c.That(importConflict?.Contains("冲突跳过 1 组", StringComparison.Ordinal) == true, "端已占用时归类为冲突而不是静默失败");

        var importUnknown = Exec(c, ctx, "map_jumpbridge_import", """{"text":"绝不存在星系XYZ ↔ 也不存在星系XYZ"}""", "map_jumpbridge_import：未识别星系归类为跳过");
        c.That(importUnknown?.Contains("未识别跳过 1 组", StringComparison.Ordinal) == true, "未识别星系被明确归类");

        var importEmpty = Exec(c, ctx, "map_jumpbridge_import", """{}""", "map_jumpbridge_import：空输入给出原因");
        c.That(importEmpty?.Contains("没有可导入的内容", StringComparison.Ordinal) == true, "空输入给明确原因");

        var importBadFile = Exec(c, ctx, "map_jumpbridge_import", """{"filePath":"Z:\\绝不存在目录\\跳桥.txt"}""", "map_jumpbridge_import：坏路径给出原因");
        c.That(importBadFile?.Contains("读取文件失败", StringComparison.Ordinal) == true, "文件读取失败被捕获并说明");

        // 导入（文件形式）：先删掉前面文本导入加的那组，确保这里验的是"文件读取 + 解析 + 写入"整条链
        if (JumpBridgeSettingService.GetBridgesDict().ContainsKey(idA) || JumpBridgeSettingService.GetBridgesDict().ContainsKey(idB))
        {
            JumpBridgeSettingService.Remove(idA, idB);
        }

        var tempFile = Path.Combine(Path.GetTempPath(), $"dsh-probe-jumpbridge-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(tempFile, "吉他 - 皮尔米特" + Environment.NewLine);
            var fromFile = Exec(c, ctx, "map_jumpbridge_import", $$"""{"filePath":{{JsonSerializer.Serialize(tempFile)}}}""", "map_jumpbridge_import：文件形式导入");
            c.That(fromFile?.Contains("新增 1 组", StringComparison.Ordinal) == true,
                $"文件形式导入同样生效（「A - B」写法，实际「{Short(fromFile)}」）");
            c.That(JumpBridgeSettingService.GetBridgesDict().ContainsKey(idA), "文件导入后配置里确实有这组跳桥");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }

        // 收尾：把测试加的跳桥删掉，避免残留（finally 里还有整份配置的兜底还原）
        var cleanup = JumpBridgeSettingService.GetBridgesDict();
        if (cleanup.ContainsKey(idA) || cleanup.ContainsKey(idB))
        {
            JumpBridgeSettingService.Remove(idA, idB);
        }
    }

    // ============================ 星图页真实应用 ============================

    private static void PageAppliesCommand(Check c)
    {
        var jita = ToolNameResolver.ResolveSystem("吉他");
        if (jita is null)
        {
            c.That(false, "星系解析失败，星图页断言无法进行");
            return;
        }

        Page? page = null;
        try
        {
            page = new MapPage();   // 构造即订阅 MapCommandRequested（并解析全部 XAML）
        }
        catch (Exception ex)
        {
            c.That(false, $"MapPage 构造抛异常：{ex.Message}");
        }

        c.That(page is not null, "MapPage 构造成功（无缺失 StaticResource）");
        if (page is null)
        {
            return;
        }

        var viewModel = page.DataContext as MapPageViewModel;
        c.That(viewModel is not null, "星图页 DataContext 是 MapPageViewModel");
        if (viewModel is null)
        {
            return;
        }

        Drain();
        ProbeApplication.ClearUnhandled();
        // 星图页的图层切换是 async 事件处理器：装上调度上下文（续体回到本线程）+ 后面泵消息，
        // 这样它碰 UI 就不会跨线程崩（项目踩坑 §90；阶段 115 这里真崩过一次）
        ProbeApplication.InstallDispatcherContext();
        var command = new Navigation.MapViewCommand
        {
            Route = [jita.SolarSystemID],
            CenterSystemId = jita.SolarSystemID,
            ColorMode = MapColorMode.Sovereignty,
            ShowBridges = false,
        };

        try
        {
            Navigation.NavigateToMap(command);
            c.That(true, "星图页订阅后应用命令未抛异常（SetRoute/ToSystem 可用）");
        }
        catch (Exception ex)
        {
            c.That(false, $"星图页应用命令抛异常：{ex.GetType().Name} {ex.Message}");
        }

        // 切图层会触发 async 事件处理器（ApplyColorModeAsync）：推完队列，异步异常不能漏
        ProbeApplication.Pump(6);

        c.That(Navigation.TakePendingMapCommand() is null, "星图页已订阅时命令被即时消费（不再停留在 pending）");
        c.Equal(viewModel.ColorMode, MapColorMode.Sovereignty, "图层配色已作用到星图 VM");
        c.Equal(viewModel.ShowBridges, false, "跳桥层开关已作用到星图 VM");

        var asyncErrors = ProbeApplication.UnhandledExceptions.ToList();
        c.That(asyncErrors.Count == 0, $"切图层未产生异步未处理异常（实际 {asyncErrors.Count} 条）");
        foreach (var error in asyncErrors.Take(3))
        {
            Console.WriteLine($"    [异步异常] {error}");
        }
    }

    // ============================ 工具方法 ============================

    private static ToolContext NewContext() => new()
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
            c.That(false, $"{label}（工具未注册）");
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(argsJson);
            var result = ProbeApplication.RunBlocking(() =>
                tool.ExecuteAsync(ctx, document.RootElement.Clone(), CancellationToken.None));
            c.That(result.Text.Length > 0, label);
            return result.Text;
        }
        catch (Exception ex)
        {
            c.That(false, $"{label}（抛异常：{ex.GetType().Name} {ex.Message}）");
            return null;
        }
    }

    private static void Drain()
    {
        Navigation.TakePendingMapCommand();
        Navigation.TakePendingSystem();
        Navigation.TakePendingMarketType();
        Navigation.TakePendingCharacter();
        Navigation.TakePendingAssistantQuery();
    }

    private static bool SameBridges(IReadOnlyDictionary<int, int> left, IReadOnlyDictionary<int, int> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var other) || other != value)
            {
                return false;
            }
        }

        return true;
    }

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
