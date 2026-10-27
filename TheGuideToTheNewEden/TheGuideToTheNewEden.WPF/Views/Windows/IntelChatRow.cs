using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.EVELogs;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>预警小窗频道列表的一行（只做展示，不参与业务状态）。</summary>
public sealed class IntelChatRow
{
    public string TimeText { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
    public string SystemText { get; init; } = string.Empty;
    public string JumpText { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Meta { get; init; } = string.Empty;
    public string Kind { get; init; } = "Info";
    public string ToolTip { get; init; } = string.Empty;

    /// <summary>没有命中星系的消息不显示空的“星系 + 跳数”行，让正文垂直居中。</summary>
    public System.Windows.Visibility SystemVisibility =>
        string.IsNullOrWhiteSpace(SystemText)
            ? System.Windows.Visibility.Collapsed
            : System.Windows.Visibility.Visible;

    public static IntelChatRow FromChat(IntelChatContent content, string jumpsText, string channelFallback)
    {
        var ships = content.IntelShips?
            .Select(p => p.Ship?.TypeName)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct()
            .ToList() ?? [];

        var kind = content.IntelType == IntelChatType.Clear
            ? "Clear"
            : content.IntelType == IntelChatType.Intel
                ? "Alert"
                : "Info";

        var channel = string.IsNullOrWhiteSpace(content.ChannelName)
            ? channelFallback
            : content.ChannelName;
        var system = content.SolarSystemName ?? string.Empty;
        var jump = content.Jumps >= 0 ? $"{content.Jumps} {jumpsText}" : string.Empty;

        return new IntelChatRow
        {
            TimeText = content.LocalTime.ToString("HH:mm:ss"),
            Channel = channel,
            SystemText = system,
            JumpText = jump,
            Message = content.Content ?? string.Empty,
            Meta = ships.Count > 0 ? string.Join(" · ", ships) : kind == "Clear" ? "↺" : string.Empty,
            Kind = kind,
            ToolTip = string.IsNullOrWhiteSpace(system)
                ? content.Content ?? string.Empty
                : $"{system} {jump}\n{content.Content}",
        };
    }

    public static IntelChatRow FromWarning(EarlyWarningContent content, string channel, string jumpsText)
    {
        var ships = content.IntelShips?
            .Select(p => p.Ship?.TypeName)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct()
            .ToList() ?? [];

        return new IntelChatRow
        {
            TimeText = content.Time.ToLocalTime().ToString("HH:mm:ss"),
            Channel = channel,
            SystemText = content.SolarSystemName ?? string.Empty,
            JumpText = content.Jumps >= 0 ? $"{content.Jumps} {jumpsText}" : string.Empty,
            Message = content.Content ?? string.Empty,
            Meta = ships.Count > 0 ? string.Join(" · ", ships) : channel,
            Kind = "Kill",
            ToolTip = $"{content.SolarSystemName} {content.Jumps} {jumpsText}\n{content.Content}",
        };
    }
}
