using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using ESI.NET.Models.PlanetaryInteraction;
using Microsoft.UI.Xaml;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class ChannelTranslationWindow : ToolWindow
    {
        private const string WindowSettingKey = "ChannelTranslationWindowPosAndSize";
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
            try
            {
                int winW = 400;
                int winH = 500;
                var str = Services.Settings.SettingService.GetValue(WindowSettingKey);
                if (!string.IsNullOrEmpty(str))
                {
                    var array = str.Split(',');
                    if (array.Length == 4)
                    {
                        int x = int.Parse(array[0]);
                        int y = int.Parse(array[1]);
                        winW = int.Parse(array[2]);
                        winH = int.Parse(array[3]);
                        this.SetPosition(x, y);
                    }
                }
                this.SetSize(winW, winH);
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
            this.SetAlwaysOnTop();
            AppWindow.Changed += AppWindow_Changed;
        }

        private void AppWindow_Changed(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
        {
            if (args.DidPositionChange)
            {
                if (!Helpers.WindowHelper.IsInWindow(sender.Position.X, sender.Position.Y))
                {
                    return;//不保存位置
                }
            }
            if (args.DidPositionChange || args.DidSizeChange)
            {
                Services.Settings.SettingService.SetValue(WindowSettingKey, $"{sender.Position.X},{sender.Position.Y},{sender.Size.Width},{sender.Size.Height}");
            }
        }

        public void UpdateContent(IEnumerable<Core.Models.EVELogs.ChatContent> items, string from, string to)
        {
            _page.UpdateContent(items, from, to);
        }
        public void Remove(string listener)
        {
            _page.Remove(listener);
        }
    }
}
