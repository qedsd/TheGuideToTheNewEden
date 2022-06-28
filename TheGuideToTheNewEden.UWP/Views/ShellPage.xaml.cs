using System;

using TheGuideToTheNewEden.UWP.ViewModels;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TheGuideToTheNewEden.UWP.Views
{
    // TODO: Change the icons and titles for all NavigationViewItems in ShellPage.xaml.
    public sealed partial class ShellPage : Page
    {
        public ShellViewModel ViewModel { get; } = new ShellViewModel();
        public ShellPage()
        {
            InitializeComponent();
            DataContext = ViewModel;
            ViewModel.Initialize(shellFrame, navigationView, KeyboardAccelerators);
            var tiWtleBar = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().TitleBar;
            tiWtleBar.BackgroundColor = Colors.Transparent;
            tiWtleBar.ButtonBackgroundColor = Colors.Transparent;
            tiWtleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            Windows.ApplicationModel.Core.CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
            Windows.UI.Xaml.Window.Current.SetTitleBar(AppTitleBar);
            ThemeSelectorService_ThemeChanged(null, EventArgs.Empty);
            Services.ThemeSelectorService.ThemeChanged += ThemeSelectorService_ThemeChanged;
        }

        private void ThemeSelectorService_ThemeChanged(object sender, EventArgs e)
        {
            if(Services.ThemeSelectorService.Theme == ElementTheme.Default)
            {
                if(Application.Current.RequestedTheme == ApplicationTheme.Light)
                {
                    SetLightTitleBar();
                }
                else
                {
                    SetDarkTitleBar();
                }
            }
            else if (Services.ThemeSelectorService.Theme == ElementTheme.Dark)
            {
                SetDarkTitleBar();
            }
            else
            {
                SetLightTitleBar();
            }
        }
        private void SetLightTitleBar()
        {
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            // Set active window colors
            titleBar.ButtonForegroundColor = Colors.Black;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonHoverForegroundColor = Colors.Black;
            titleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 180, 180, 180);
            titleBar.ButtonPressedForegroundColor = Colors.Black;
            titleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 150, 150, 150);

            // Set inactive window colors
            titleBar.InactiveForegroundColor = Colors.DimGray;
            titleBar.InactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveForegroundColor = Colors.DimGray;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            titleBar.BackgroundColor = Color.FromArgb(255, 210, 210, 210);
        }
        private void SetDarkTitleBar()
        {
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            // Set active window colors
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonHoverForegroundColor = Colors.White;
            titleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 90, 90, 90);
            titleBar.ButtonPressedForegroundColor = Colors.White;
            titleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 120, 120, 120);

            // Set inactive window colors
            titleBar.InactiveForegroundColor = Colors.Gray;
            titleBar.InactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveForegroundColor = Colors.Gray;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            titleBar.BackgroundColor = Color.FromArgb(255, 45, 45, 45);
        }
    }
}
