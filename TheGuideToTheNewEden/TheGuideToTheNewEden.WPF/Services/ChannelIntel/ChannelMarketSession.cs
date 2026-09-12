using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.ChannelIntel;

/// <summary>
/// 一个角色的频道查价会话（对齐 WinUI 版 <c>Models/ChannelMarket</c>）：
/// 为每个勾选频道创建 Core 的 <see cref="ChannelMarketObserver"/>（增量读日志、
/// 触发关键词过滤、分隔符拆分物品名、本地 SDE 匹配市场物品），
/// 命中查价请求后交 <see cref="ChannelMarketService.Current"/> 查询并展示在置顶结果窗。
/// </summary>
public sealed class ChannelMarketSession
{
    private readonly List<ChannelMarketObserver> _observers = [];

    public ChannelMarketSetting Setting { get; }

    public bool Running { get; private set; }

    public ChannelMarketSession(string characterName)
    {
        Setting = ChannelMarketSettingService.GetValue(characterName)
            ?? new ChannelMarketSetting { CharacterName = characterName };
    }

    /// <summary>页面勾选的频道（日志文件路径）。</summary>
    public void SetSelectedChannels(IEnumerable<string> paths)
        => Setting.Channels = paths.ToList();

    public void Start()
    {
        _observers.Clear();
        foreach (var path in Setting.Channels)
        {
            var observer = new ChannelMarketObserver(path, Setting.CharacterName, Setting.KeyWord, Setting.ItemsSeparator);
            if (Core.Services.ObservableFileService.Add(observer))
            {
                observer.OnContentUpdate += Observer_OnContentUpdate;
                _observers.Add(observer);
            }
        }

        Save();
        Running = true;
    }

    private void Observer_OnContentUpdate(ChannelMarketObserver sender, IEnumerable<MarketChatContent> news)
    {
        var hits = news.Where(p => p.Important).ToList();
        if (hits.Count > 0)
        {
            ChannelMarketService.Current.Query(hits, Setting.MarketRegionID);
        }
    }

    public void Stop()
    {
        Core.Services.ObservableFileService.Remove(_observers);
        _observers.Clear();
        Running = false;
    }

    public void Save() => ChannelMarketSettingService.SetValue(Setting);
}
