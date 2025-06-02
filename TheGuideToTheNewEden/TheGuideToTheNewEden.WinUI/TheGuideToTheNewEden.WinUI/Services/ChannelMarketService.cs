using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Services
{
    internal class ChannelMarketService
    {
        private static ChannelMarketService current;
        public static ChannelMarketService Current
        {
            get
            {
                current ??= new ChannelMarketService();
                return current;
            }
        }

        private Wins.ChannelMarketWindow _window;
        private int _count;

        public void Start()
        {
            _count++;
            _window ??= new Wins.ChannelMarketWindow();
        }
        public void Stop()
        {
            _count--;
            if (_count == 0)
            {
                _window?.Close();
                _window = null;
            }
        }
        public void Query(IEnumerable<MarketChatContent> items, int regionID)
        {
            if (items == null || !items.Any())
                return;
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                _window.Activate();
                _window.SetForegroundWindow();
                _window.UpdateContent(items, regionID);
            });
        }

        public void RestorePos()
        {
            if(_window!= null)
            {
                Helpers.WindowHelper.CenterToScreen(_window);
            }
        }
    }
}
