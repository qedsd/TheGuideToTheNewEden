using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using TheGuideToTheNewEden.WinUI.Extensions;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Services
{
    internal class ChannelTranslationService: IService
    {
        private Wins.ChannelTranslationWindow _window;
        private int _count;

        public void Start()
        {
            _count++;
            _window ??= new Wins.ChannelTranslationWindow();
        }
        public void Stop(string listener)
        {
            _count--;
            _window.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                _window?.Remove(listener);
                if (_count == 0)
                {
                    _window?.Close();
                    _window = null;
                }
            });
        }
        public void Query(IEnumerable<Core.Models.EVELogs.ChatContent> items, string from , string to)
        {
            if (items == null || !items.Any())
                return;
            _window.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                _window.Activate();
                _window.SetForegroundWindow();
                _window.UpdateContent(items, from, to);
            });
        }

        public void RestorePos()
        {
            if(_window!= null)
            {
                Helpers.WindowHelper.CenterToScreen(_window);
            }
        }

        public void Init()
        {
            
        }

        public void Dispose()
        {
            _count = 0;
            _window?.Close();
            _window = null;
        }
    }
}
