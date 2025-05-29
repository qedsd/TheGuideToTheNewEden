using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUICommunity;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    public sealed partial class ChannelMarketWindow : Window
    {
        private readonly WinUICommunity.ThemeService _themeService;
        public ChannelMarketWindow()
        {
            this.InitializeComponent();
            SetTitleBar(AppTitleBarGrid);
            ExtendsContentIntoTitleBar = true;
            Helpers.WindowHelper.GetAppWindow(this).SetIcon(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo_32.ico"));
            this.Title = Helpers.ResourcesHelper.GetString("ShellPage_ChannelMarket");
            this.ExtendsContentIntoTitleBar = true;
            var presenter = Helpers.WindowHelper.GetOverlappedPresenter(this);
            presenter.SetBorderAndTitleBar(true, false);
            _themeService = new WinUICommunity.ThemeService();
            _themeService.Initialize(this);
            _themeService.ConfigElementTheme(ThemeSelectorService.Theme);
            _themeService.ConfigBackdrop();
            ThemeSelectorService.OnChangedTheme += ThemeSelectorService_OnChangedTheme;
            ThemeSelectorService_OnChangedTheme(ThemeSelectorService.Theme);
        }
        private void ThemeSelectorService_OnChangedTheme(ElementTheme theme)
        {
            if (BackdropSelectorService.BackdropTypeValue == BackdropSelectorService.BackdropType.None)
            {
                switch (theme)
                {
                    case ElementTheme.Dark: MainWindowGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32)); break;
                    case ElementTheme.Light: MainWindowGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 243, 243)); break;
                    case ElementTheme.Default:
                        {
                            if (Application.Current.RequestedTheme == ApplicationTheme.Dark)
                            {
                                MainWindowGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32));
                            }
                            else
                            {
                                MainWindowGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 243, 243));
                            }
                        }
                        break;
                }
            }
        }
        private void Button_Top_Click(object sender, RoutedEventArgs e)
        {

        }

        private void _Button_Close_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
