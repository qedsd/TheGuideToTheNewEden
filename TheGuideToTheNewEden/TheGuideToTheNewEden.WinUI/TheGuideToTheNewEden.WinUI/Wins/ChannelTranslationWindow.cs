using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using ESI.NET.Models.PlanetaryInteraction;
using Microsoft.UI.Xaml;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class ChannelTranslationWindow : ToolWindow
    {
        private Views.ChannelTranslationWinPage _page;
        private string _title;
        public ChannelTranslationWindow()
        {
            _page = new Views.ChannelTranslationWinPage();
            _title = Helpers.ResourcesHelper.GetString("ChannelTranslationPage");
            InitWindow(_page, WindowTitleStyle.OnlyClose, true, true, true, true);

            SetDisplayTitle(_title);
            SetWindowTitle(Title);
            SetCloseToHide();

            _page.SetWindow(this);
            this.SetSize(600, 600);
            this.SetAlwaysOnTop();
        }
        public void UpdateContent(IEnumerable<Core.Models.EVELogs.ChatContent> items, string from, string to)
        {
            _page.UpdateContent(items, from, to);
        }
    }
}
