using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Assistant;

namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// 探针专用工具：注册进真实 <see cref="ToolRegistry"/>，用来**确定性地**触发引擎的各条分支
/// （成功 / 写操作挂起 / 抛异常 / 超时 / 可取消），而这些分支靠真实模型是撞不出来的。
/// </summary>
internal static class ProbeTools
{
    public static int EchoCalls;
    public static int WriteCalls;
    public static int ThrowCalls;
    public static int TimeoutCalls;
    public static int SlowCalls;
    public static int LongCalls;

    /// <summary>超长结果里的尾部标记（引擎应把它截断掉，探针据此断言 4000 字符上限）。</summary>
    public const string LongTailMarker = "ZZZ-END-OF-LONG-RESULT";

    public static void Register()
    {
        ToolRegistry.Register(new ProbeTool("probe_echo", requiresConfirmation: false, kind: ProbeKind.Echo));
        ToolRegistry.Register(new ProbeTool("probe_write", requiresConfirmation: true, kind: ProbeKind.Write));
        ToolRegistry.Register(new ProbeTool("probe_throw", requiresConfirmation: false, kind: ProbeKind.Throw));
        ToolRegistry.Register(new ProbeTool("probe_timeout", requiresConfirmation: false, kind: ProbeKind.Timeout));
        ToolRegistry.Register(new ProbeTool("probe_slow", requiresConfirmation: false, kind: ProbeKind.Slow));
        ToolRegistry.Register(new ProbeTool("probe_long", requiresConfirmation: false, kind: ProbeKind.Long));
    }

    public static void ResetCounters()
    {
        EchoCalls = WriteCalls = ThrowCalls = TimeoutCalls = SlowCalls = LongCalls = 0;
    }

    private enum ProbeKind
    {
        Echo,
        Write,
        Throw,
        Timeout,
        Slow,
        Long,
    }

    private sealed class ProbeTool : ITool
    {
        private readonly ProbeKind _kind;

        public ProbeTool(string name, bool requiresConfirmation, ProbeKind kind)
        {
            Name = name;
            RequiresConfirmation = requiresConfirmation;
            _kind = kind;
        }

        public string Name { get; }

        public string DisplayNameKey => $"AssistantTool_{Name}";

        public string Description => "探针专用工具（仅验收用）。";

        public JsonObject? Parameters => new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { ["value"] = new JsonObject { ["type"] = "string" } },
        };

        public bool RequiresConfirmation { get; }

        public bool IsAction => false;

        public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
        {
            var value = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("value", out var node)
                ? node.GetString() ?? string.Empty
                : string.Empty;

            switch (_kind)
            {
                case ProbeKind.Echo:
                    EchoCalls++;
                    return new ToolResult($"echo: {value}", ParamSummary: $"value={value}");

                case ProbeKind.Write:
                    WriteCalls++;
                    return new ToolResult($"write applied: {value}", ParamSummary: $"value={value}");

                case ProbeKind.Throw:
                    ThrowCalls++;
                    throw new InvalidOperationException("探针故意抛出的异常");

                case ProbeKind.Timeout:
                    TimeoutCalls++;
                    // 与"用户取消"区分：抛的 OperationCanceledException **不带**用户的令牌
                    throw new OperationCanceledException("探针模拟工具自身超时");

                case ProbeKind.Long:
                    LongCalls++;
                    return new ToolResult(new string('x', 5000) + LongTailMarker, ParamSummary: "超长结果");

                default:
                    SlowCalls++;
                    // 一直等到用户取消（用于验证取消传播）
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                    return new ToolResult("slow finished");
            }
        }
    }
}
