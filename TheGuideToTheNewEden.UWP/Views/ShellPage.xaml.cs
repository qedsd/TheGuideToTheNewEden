using System;

using TheGuideToTheNewEden.UWP.ViewModels;
using Windows.UI;
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
        }
    }
}
