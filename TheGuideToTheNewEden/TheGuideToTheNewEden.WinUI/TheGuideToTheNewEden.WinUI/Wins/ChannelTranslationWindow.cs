using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using ESI.NET.Models.PlanetaryInteraction;
using Microsoft.UI.Xaml;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class ChannelTranslationWindow : ToolWindow
    {
        private Views.ChannelTranslationWinPage _page;
        public ChannelTranslationWindow()
        {
            _page = new Views.ChannelTranslationWinPage();
            string title = Helpers.ResourcesHelper.GetString("ChannelTranslationPage");
            InitWindow(_page, WindowTitleStyle.OnlyClose, true, true, true, true);

            SetDisplayTitle(title);
            SetWindowTitle(title);
            SetCloseToHide();

            _page.SetWindow(this);
            
            this.SetAlwaysOnTop();
            this.LogPositionAndSize();
        }

        public void UpdateContent(IEnumerable<Core.Models.EVELogs.ChatContent> items, string from, string to)
        {
            _page.UpdateContent(items, from, to);
        }
        public void UpdateLimtedContent(IEnumerable<Core.Models.EVELogs.ChatContent> items)
        {
            _page.UpdateLimitedContent(items);
        }
        public void Remove(string listener)
        {
            _page.Remove(listener);
        }
    }
}
