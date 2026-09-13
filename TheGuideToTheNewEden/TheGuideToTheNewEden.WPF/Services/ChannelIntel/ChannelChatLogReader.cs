using System.IO;
using System.Text;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models.EVELogs;

namespace TheGuideToTheNewEden.WPF.Services.ChannelIntel;

/// <summary>
/// 读聊天日志的**尾部若干行**并解析成 <see cref="ChatContent"/>。
/// <para>
/// 用途：Core 的观察者是从"启动时文件末尾"开始增量读的，因此**启动前**已经写进日志的内容
/// （最典型的就是加入频道时写下的「频道置顶信息 / Channel MOTD」）永远不会被翻译。
/// 会话启动时用本方法把最近的若干条补翻一次，用户点「开始」就能立刻看到置顶信息的译文。
/// </para>
/// </summary>
public static class ChannelChatLogReader
{
    /// <summary>默认补翻行数（够覆盖 MOTD 与最近几句，又不至于一启动就烧一堆 token）。</summary>
    public const int DefaultSeedLines = 20;

    /// <summary>
    /// 读取日志尾部最多 <paramref name="maxLines"/> 行，解析成消息列表。
    /// 关键词规则与 Core 观察者一致：设了关键词则只保留命中的消息（<see cref="ChatContent.Important"/>）。
    /// </summary>
    public static List<ChatContent> ReadRecent(string path, string listenerName, string? keyword, int maxLines = DefaultSeedLines)
    {
        var results = new List<ChatContent>();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || maxLines <= 0)
        {
            return results;
        }

        try
        {
            var info = GameLogHelper.GetChatChanelInfo(path);
            var lines = File.ReadLines(path, Encoding.Unicode).TakeLast(maxLines);
            foreach (var line in lines)
            {
                var content = ChatContent.Create(line);
                if (content is null || string.IsNullOrWhiteSpace(content.Content))
                {
                    continue;
                }

                content.Listener = listenerName;
                content.ChannelName = info?.ChannelName ?? string.Empty;
                content.ChannelID = info?.UID ?? string.Empty;

                if (!string.IsNullOrEmpty(keyword) && content.Content.IndexOf(keyword, StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                content.Important = true;
                results.Add(content);
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return results;
    }
}
