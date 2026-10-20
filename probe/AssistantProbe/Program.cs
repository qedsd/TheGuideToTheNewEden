namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// AI 助手探针入口。用法：
/// <code>
/// dotnet run --project probe/AssistantProbe --            # 全部套件
/// dotnet run --project probe/AssistantProbe -- sde        # SDEBuilder 离线构建与字段/表断言（阶段 157）
/// dotnet run --project probe/AssistantProbe -- toolcall   # 协议层工具调用形状（离线）
/// dotnet run --project probe/AssistantProbe -- engine     # 引擎循环（本地 mock 大模型）
/// dotnet run --project probe/AssistantProbe -- tools      # 工具 Schema 与执行（真 SDE 库）
/// dotnet run --project probe/AssistantProbe -- quickroute # 主页快捷输入路由 + pending 语义
/// dotnet run --project probe/AssistantProbe -- map        # 星图命令通道 + 跳桥写工具（会写盘后还原）
/// dotnet run --project probe/AssistantProbe -- business   # 商业重型工具的进度/取消
/// dotnet run --project probe/AssistantProbe -- writes     # 阶段 116 三个写工具（会写盘，跑完还原）
/// dotnet run --project probe/AssistantProbe -- esi        # 阶段 118 ESI 通用网关（索引/检索/URL/整形/工具面；末尾一次匿名联网冒烟）
/// dotnet run --project probe/AssistantProbe -- tokens     # 阶段 122 工具声明的 token 预算与缓存友好约束
/// dotnet run --project probe/AssistantProbe -- routing    # 阶段 123 按需声明（工具套餐路由）
/// dotnet run --project probe/AssistantProbe -- manuals    # 阶段 131 内置说明书（结构 / 检索分片 / AI 接线）
/// dotnet run --project probe/AssistantProbe -- ui         # 界面构造与绑定（离屏窗口）
/// </code>
/// 退出码：0 = 全 PASS，1 = 有 FAIL，2 = 探针自身异常。
/// <para><b>构建务必重定向输出目录</b>（见 .csproj 头注释），否则会与 VS 的构建互相写坏主工程产物。</para>
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch (Exception)
        {
            // 控制台不支持设置编码时忽略
        }

        var suite = args.FirstOrDefault()?.Trim().ToLowerInvariant() ?? "all";
        Console.WriteLine($"AI 助手探针：套件 = {suite}");

        ProbeTools.Register();

        var check = new Check();
        try
        {
            if (suite is "all" or "sde")
            {
                SdeSuite.Run(check);
            }

            if (suite is "all" or "toolcall")
            {
                ToolCallSuite.Run(check);
            }

            if (suite is "all" or "engine")
            {
                EngineSuite.RunAsync(check).GetAwaiter().GetResult();
            }

            if (suite is "all" or "tools")
            {
                ToolSuite.Run(check);
            }

            if (suite is "all" or "quickroute")
            {
                QuickRouteSuite.Run(check);
            }

            if (suite is "all" or "map")
            {
                MapProbe.Run(check);
            }

            if (suite is "all" or "business")
            {
                BusinessSuite.Run(check);
            }

            if (suite is "all" or "writes")
            {
                WriteToolSuite.Run(check);
            }

            if (suite is "all" or "esi")
            {
                EsiSuite.Run(check);
            }

            if (suite is "all" or "tokens")
            {
                TokenSuite.Run(check);
            }

            if (suite is "all" or "routing")
            {
                RoutingSuite.Run(check);
            }

            if (suite is "all" or "manuals")
            {
                ManualSuite.Run(check);
            }

            if (suite is "all" or "ui")
            {
                UiSuite.Run(check);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[探针自身异常] {ex}");
            return 2;
        }

        Console.WriteLine($"\n===== 汇总：PASS {check.Pass} / FAIL {check.Fail} =====");
        foreach (var failure in check.Failures)
        {
            Console.WriteLine($"  失败：{failure}");
        }

        return check.Fail == 0 ? 0 : 1;
    }
}
