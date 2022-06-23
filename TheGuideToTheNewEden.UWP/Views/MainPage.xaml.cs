using System;

using TheGuideToTheNewEden.UWP.ViewModels;

using Windows.UI.Xaml.Controls;

namespace TheGuideToTheNewEden.UWP.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; } = new MainViewModel();

        public MainPage()
        {
            InitializeComponent();
        }
    }
}
