using ESI.NET.Models.Fleets;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Helpers;
using Vanara.PInvoke;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class GamePreviewOverlapWindow : Window
    {
        private ComCtl32.SUBCLASSPROC wndProcHandler;
        private Grid _grid;
        private TextBlock _textBlock;
        private readonly AppWindow _appWindow;
        public GamePreviewOverlapWindow()
        {
            _textBlock = new TextBlock()
            {
                FontSize = 18,
                Margin = new Thickness(0)
            };
            _textBlock.Loaded += _textBlock_Loaded;
            //_grid = new Grid();
            //_grid.Children.Add(_textBlock);
            Content = _textBlock;
            Helpers.WindowHelper.HideTitleBar2(this);
            _appWindow = Helpers.WindowHelper.GetAppWindow(this);
            this.Activated += GamePreviewOverlapWindow_Activated;
        }

        private void _textBlock_Loaded(object sender, RoutedEventArgs e)
        {
            _textBlock.Loaded -= _textBlock_Loaded;
            var dpi = WindowHelper.GetDpiScale(this);
            Resize((int)Math.Ceiling(_textBlock.ActualWidth * dpi), (int)Math.Ceiling(_textBlock.ActualHeight * dpi));
        }
        private void GamePreviewOverlapWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            this.Activated -= GamePreviewOverlapWindow_Activated;
        }

        public void SetTitle(string title)
        {
            _textBlock.Text = title;
        }
        public void Move(int x, int y)
        {
            Helpers.WindowHelper.MoveToScreen(this, x + 10, y + 10);
        }
        public void Resize(int w, int h)
        {
            _appWindow.Resize(new Windows.Graphics.SizeInt32(w, h));
        }
        public void Show()
        {
            this.Activate();
            Helpers.WindowHelper.TopMost(this);
        }

        
    }
}
