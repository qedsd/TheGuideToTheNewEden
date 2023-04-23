using System;

using TheGuideToTheNewEden.UWP.ViewModels;

using Windows.UI.Xaml.Controls;

namespace TheGuideToTheNewEden.UWP.Views
{
    public sealed partial class HomePage : Page
    {
        public HomeViewModel ViewModel { get; } = new HomeViewModel();

        public HomePage()
        {
            InitializeComponent();
        }
    }
}
