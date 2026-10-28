using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.ChannelIntel;

/// <summary>
/// 启动频道类监控前，按当前日志目录重新扫描一次，把已有频道项的文件路径更新到最新日期的文件。
/// 游戏重启后会新建聊天日志文件，页面缓存里的旧路径不会自动变化，直接开始会监听旧文件。
/// </summary>
public static class ChatlogChannelRefreshService
{
    public static async Task RefreshAsync(string listener, IEnumerable<ChatChanelInfoItem>? channels)
    {
        var items = channels?.ToList();
        if (items is not { Count: > 0 } || string.IsNullOrWhiteSpace(listener))
        {
            return;
        }

        try
        {
            var logPath = GameLogsSettingService.GetChatlogsPath();
            if (!System.IO.Directory.Exists(logPath))
            {
                return;
            }

            var latest = await Task.Run(() => GameLogHelper.GetChatChanelInfos(
                logPath,
                Math.Max(1, GameLogsSettingService.EVELogsChannelDurationValue)));
            if (latest is null || !latest.TryGetValue(listener, out var latestChannels))
            {
                return;
            }

            foreach (var item in items)
            {
                var newest = latestChannels.FirstOrDefault(p => p.ChannelID == item.ChannelID);
                if (newest is null)
                {
                    continue;
                }

                item.Info.FilePath = newest.FilePath;
                item.Info.ChannelName = newest.ChannelName;
                item.Info.SessionStarted = newest.SessionStarted;
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }
}
