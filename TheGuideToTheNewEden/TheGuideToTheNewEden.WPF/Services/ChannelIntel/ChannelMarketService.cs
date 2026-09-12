using System.Windows;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Services.ChannelIntel;

/// <summary>
/// 频道查价的编排服务（对齐 WinUI 版 <c>Services/ChannelMarketService</c>）：
/// 持有常驻置顶结果窗（<see cref="ChannelMarketWindow"/>，关闭即隐藏、复用），
/// 收到查价请求时把窗口弹到前台并按市场星域更新报价内容。
/// </summary>
public sealed class ChannelMarketService
{
    public static ChannelMarketService Current { get; } = new();

    private ChannelMarketWindow? _window;
    private int _count;

    private ChannelMarketService()
    {
    }

    /// <summary>引用计数启动（首个会话启动时创建结果窗）。</summary>
    public void Start()
    {
        _count++;
        if (_window is null)
        {
            _window = new ChannelMarketWindow();
        }
    }

    public void Stop()
    {
        _count = Math.Max(0, _count - 1);
        if (_count == 0)
        {
            _window?.HideWindow();
        }
    }

    /// <summary>查询报价并展示（contents 为命中市场物品的频道消息）。</summary>
    public void Query(IEnumerable<MarketChatContent> contents, int regionId)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || _window is null)
        {
            return;
        }

        _ = dispatcher.BeginInvoke(async () =>
        {
            if (_window is null)
            {
                return;
            }

            var regionName = await Core.Services.DB.MapRegionService.QueryAsync(regionId);
            _window.Query(contents, regionId, regionName?.RegionName ?? regionId.ToString());
        });
    }

    public void RestorePos()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.Invoke(() => _window?.RestorePos());
    }
}
