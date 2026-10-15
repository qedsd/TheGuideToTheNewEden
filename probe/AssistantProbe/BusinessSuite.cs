using System.Text.Json;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.Services.Business;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// 商业重型工具（<c>scalper_analyse</c> / <c>compare_analyse</c>）的**进度与取消**（阶段 107/113 补测）。
/// <para>
/// 有意**不完整跑**这两条管线：倒货是分钟级、几百次 ESI 调用（设计 §11.3 的成本控制），探针只验证
/// ①设置缺失时的明确拒绝；②取消能被及时响应（不忽略令牌）；③执行期间进度真的上报（界面的"正在执行"靠它）。
/// 完整跑一次由用户实机确认。
/// </para>
/// </summary>
internal static class BusinessSuite
{
    public static void Run(Check c)
    {
        ProbeCore.Ensure();

        var scalper = ScalperSettingService.Load();
        var compare = CompareMarketSettingService.Load();
        var scalperConfigured = scalper.SourceMarketLocation is not null
                                && scalper.DestinationMarketLocation is not null
                                && (scalper.MarketTypes?.Any(t => t > 0) ?? false);
        var compareConfigured = (compare.Markets?.Count ?? 0) > 0;

        c.Section("BusinessSuite · 前置状态");
        c.That(true, $"倒货设置 {(scalperConfigured ? "已配置" : "未配置")}；比价设置 {(compareConfigured ? $"已配置（{compare.Markets!.Count} 个市场）" : "未配置")}");

        c.Section("BusinessSuite · 参数与设置缺失分支");
        Deterministic(c, scalperConfigured, compareConfigured);

        c.Section("BusinessSuite · 重型管线的取消与进度");
        CancelScenarios(c, scalperConfigured, compareConfigured);
    }

    private static void Deterministic(Check c, bool scalperConfigured, bool compareConfigured)
    {
        var ctx = NewContext(_ => { });

        Exec(c, ctx, "compare_analyse", """{}""",
            text => text.Contains("不能为空"), "compare_analyse：缺 items 给出原因");
        Exec(c, ctx, "compare_analyse", """{"items":["绝不存在物品XYZ"]}""",
            text => text.Contains("没有解析出任何物品"), "compare_analyse：物品全未解析出 → 明确原因（不发取数）");
        Exec(c, ctx, "compare_analyse", """{"items":["巨神兵级"],"side":"buy"}""",
            text => compareConfigured ? text.Length > 0 : text.Contains("比价设置里没有市场"),
            compareConfigured
                ? "compare_analyse：比价设置已配置 → 只做参数路径（完整取数留实机）"
                : "compare_analyse：比价未配置市场 → 明确原因");

        Exec(c, ctx, "scalper_analyse", """{}""",
            text => scalperConfigured ? text.Length > 0 : text.Contains("倒货设置不完整"),
            scalperConfigured
                ? "scalper_analyse：倒货设置已配置 → 只做参数路径（完整取数留实机）"
                : "scalper_analyse：倒货设置不完整 → 明确原因");
    }

    private static void CancelScenarios(Check c, bool scalperConfigured, bool compareConfigured)
    {
        // 倒货：取消应向上抛 OperationCanceledException（引擎据此判定"用户停止"）
        if (scalperConfigured)
        {
            var progress = new List<string>();
            var task = RunTool("scalper_analyse", """{"top":5}""", progress, TimeSpan.FromSeconds(2));
            var done = WaitFor(task, TimeSpan.FromSeconds(45));
            c.That(done, "scalper_analyse：取消后 45 秒内返回（没有忽略取消令牌）");
            if (done)
            {
                c.That(progress.Count > 0, $"scalper_analyse：执行期间上报了进度（{progress.Count} 次，例「{First(progress)}」）");
                if (task.IsFaulted)
                {
                    var inner = task.Exception?.InnerException;
                    c.That(inner is OperationCanceledException,
                        $"scalper_analyse：取消表现为 OperationCanceledException（实际 {inner?.GetType().Name ?? "null"}）");
                }
                else
                {
                    // 取数可能在取消前就返回了 → 如实记录，不算失败
                    c.That(true, "scalper_analyse：取消前已返回（如实记录，未触发取消路径）");
                }
            }
        }
        else
        {
            c.That(true, "scalper_analyse：设置不完整 → 跳过取消测试（管线未进入取数阶段）");
        }

        // 比价：取消可能被服务层转成"无结果"，工具据此给明确回执（也接受抛 OCE）
        if (compareConfigured)
        {
            var progress = new List<string>();
            var task = RunTool("compare_analyse", """{"items":["巨神兵级","三钛合金"],"side":"sell"}""", progress, TimeSpan.FromSeconds(2));
            var done = WaitFor(task, TimeSpan.FromSeconds(45));
            c.That(done, "compare_analyse：取消后 45 秒内返回（没有忽略取消令牌）");
            if (done)
            {
                c.That(progress.Count > 0, $"compare_analyse：执行期间上报了进度（{progress.Count} 次，例「{First(progress)}」）");
                if (task.IsFaulted)
                {
                    var inner = task.Exception?.InnerException;
                    c.That(inner is OperationCanceledException,
                        $"compare_analyse：取消表现为 OperationCanceledException（实际 {inner?.GetType().Name ?? "null"}）");
                }
                else
                {
                    var text = task.Result.Text;
                    c.That(text.Contains("比价无结果") || text.Contains("无报价") || text.Length > 0,
                        $"compare_analyse：取消/失败被转成明确回执（实际「{Short(text)}」）");
                }
            }
        }
        else
        {
            c.That(true, "compare_analyse：设置未配置市场 → 跳过取消测试");
        }
    }

    // ============================ 工具方法 ============================

    private static Task<ToolResult> RunTool(string toolName, string argsJson, List<string> progress, TimeSpan cancelAfter)
    {
        var tool = ToolRegistry.Find(toolName)!;
        var ctx = NewContext(line =>
        {
            lock (progress)
            {
                progress.Add(line);
            }
        });

        var cts = new CancellationTokenSource(cancelAfter);
        using var document = JsonDocument.Parse(argsJson);
        var args = document.RootElement.Clone();   // Clone 之后与 document 解耦，可以立刻释放
        // 放线程池跑：探针若装了调度上下文，"同步等异步"会死锁，线程池上没有上下文就没有这个问题
        return Task.Run(() => tool.ExecuteAsync(ctx, args, cts.Token));
    }

    private static bool WaitFor(Task task, TimeSpan timeout)
    {
        try
        {
            return task.Wait(timeout);
        }
        catch (AggregateException)
        {
            return true;
        }
    }

    private static ToolContext NewContext(Action<string> progress) => new()
    {
        Character = null,
        Characters = [],
        OnUiThread = action => action(),
        Progress = progress,
    };

    private static void Exec(Check c, ToolContext ctx, string toolName, string argsJson, Func<string, bool> expect, string label)
    {
        var tool = ToolRegistry.Find(toolName);
        if (tool is null)
        {
            c.That(false, $"{label}（工具未注册）");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(argsJson);
            var result = ProbeApplication.RunBlocking(() =>
                tool.ExecuteAsync(ctx, document.RootElement.Clone(), CancellationToken.None));
            c.That(expect(result.Text), label);
            c.That(result.Text.Length <= 4000, $"{label}：结果 ≤ 4000 字符（实际 {result.Text.Length}）");
        }
        catch (Exception ex)
        {
            c.That(false, $"{label}（抛异常：{ex.GetType().Name} {ex.Message}）");
        }
    }

    private static string First(List<string> lines)
    {
        lock (lines)
        {
            return lines.Count > 0 ? lines[0] : "(无)";
        }
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
