using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.WPF.Models.Tools;
using TheGuideToTheNewEden.WPF.ViewModels;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 服务器测速（TCPing）：对 EVE 服务器做 N 次 TCP 连接测速，给出平均/最低/最高延迟、丢包率与档位。
/// <para>
/// 与「工具 → 服务器测速」页同一套口径（阈值与档位判定复用 <see cref="ServerPingPageViewModel"/> 的公开常量与
/// <c>DescribeLatency</c> / <c>DescribeLoss</c>，只有一处来源）。**多采样**而不是单次：单次 TCP 握手受瞬时
/// 网络抖动影响很大，"哪个服更卡"必须看平均与丢包。失败（连不上/超时）按丢包计，不写日志（那是正常结果）。
/// </para>
/// </summary>
public sealed class ServerPingTool : ITool
{
    /// <summary>默认端口（EVE 客户端 TCP 端口，与测速页一致）。</summary>
    private const int DefaultPort = ServerPingPageViewModel.DefaultPort;

    /// <summary>单次连接超时（与测速页一致）。</summary>
    private const int PingTimeoutMs = 5000;

    /// <summary>不指定 host 时同时测的两个官方服务器（欧服 / 国服）。</summary>
    private static readonly (string Name, string Host)[] Presets =
    [
        ("欧服 Tranquility", "tranquility.servers.eveonline.com"),
        ("国服 Serenity", "42.186.79.5"),
    ];

    public string Name => "server_ping";

    public string DisplayNameKey => "AssistantTool_server_ping";

    public string Description =>
        "对 EVE 服务器做 TCP 连接测速（TCPing），返回平均/最低/最高延迟、丢包率与档位。适合「现在欧服卡不卡」「哪个服延迟最低」。" +
        "不填主机 = 欧服与国服各测一次，次数越多越准也越慢；延迟只反映到登录服务器的网络质量，不代表游戏内体验。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["host"] = new JsonObject { ["type"] = "string", ["description"] = "主机名或 IP；留空 = 欧服 + 国服各测一次" },
            ["port"] = new JsonObject { ["type"] = "integer", ["description"] = "端口（默认 26000）" },
            ["samples"] = new JsonObject { ["type"] = "integer", ["description"] = "测速次数（默认 5，上限 10）" },
            ["timeoutMs"] = new JsonObject { ["type"] = "integer", ["description"] = "超时毫秒（默认 5000，上限 10000）" },
        },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var host = ToolArgs.ReadString(args, "host")?.Trim() ?? string.Empty;
        var port = ToolArgs.ReadInt(args, "port", DefaultPort);
        var samples = Math.Clamp(ToolArgs.ReadInt(args, "samples", 5), 1, 10);
        var timeoutMs = Math.Clamp(ToolArgs.ReadInt(args, "timeoutMs", PingTimeoutMs), 500, 10000);

        if (port is <= 0 or > 65535)
        {
            return new ToolResult($"端口 {port} 非法（应在 1–65535）。");
        }

        var targets = host.Length > 0
            ? [(Name: host, Host: host)]
            : Presets;

        var builder = new StringBuilder();
        builder.AppendLine($"TCPing {samples} 次/目标 · 端口 {port} · 超时 {timeoutMs}ms");

        var best = double.MaxValue;
        var bestName = string.Empty;

        foreach (var target in targets)
        {
            ct.ThrowIfCancellationRequested();

            var results = new List<double>(samples);
            for (var i = 0; i < samples; i++)
            {
                ct.ThrowIfCancellationRequested();
                // 失败返回 -1，不抛异常也不写 ERROR（连不上就是丢包，与测速页一致）
                results.Add(await TCPingHelper.PingAsync(target.Host, port, timeoutMs, logErrors: false, ct));
            }

            var ok = results.Where(v => v >= 0).ToList();
            var lost = samples - ok.Count;
            var lossPercent = lost * 100.0 / samples;

            if (ok.Count == 0)
            {
                builder.AppendLine($"- {target.Name}（{target.Host}）：全部 {samples} 次连接失败/超时 → 丢包 100%（{ServerPingPageViewModel.DescribeQuality(PingQuality.Bad)}）。" +
                    "可能原因：网络不通、被防火墙拦截、或该服务器当前不可达。");
                continue;
            }

            var average = ok.Average();
            if (average < best)
            {
                best = average;
                bestName = target.Name;
            }

            builder.AppendLine(
                $"- {target.Name}（{target.Host}）：平均 {average:F0}ms · 最低 {ok.Min():F0}ms · 最高 {ok.Max():F0}ms · " +
                $"成功 {ok.Count}/{samples} · 丢包 {lossPercent:F0}%（{ServerPingPageViewModel.DescribeQuality(ServerPingPageViewModel.DescribeLoss(lossPercent))}）；" +
                $"延迟档位 {ServerPingPageViewModel.DescribeQuality(ServerPingPageViewModel.DescribeLatency(average))}");

            if (ok.Count < samples)
            {
                builder.AppendLine($"  另有 {lost} 次连接失败（已计入丢包）。");
            }
        }

        if (bestName.Length > 0)
        {
            builder.Append($"平均延迟最低：{bestName}（约 {best:F0}ms）。");
        }

        return new ToolResult(builder.ToString().TrimEnd(),
            ParamSummary: host.Length > 0 ? $"{host}:{port} × {samples}" : $"欧服/国服 × {samples}");
    }
}
