using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.ViewModels;
using TheGuideToTheNewEden.WinUI.Views.Character;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class CharacterPage : Page
    {
        public CharacterPage()
        {
            this.InitializeComponent();
            Loaded += CharacterPage_Loaded;
        }

        private void CharacterPage_Loaded(object sender, RoutedEventArgs e)
        {
            (DataContext as BaseViewModel).Window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            switch((args.SelectedItem as NavigationViewItem).Tag)
            {
                case "Overview":ContentFrame.Navigate(typeof(OverviewPage));break;
                case "Skill": ContentFrame.Navigate(typeof(SkillPage)); break;
                case "Clone": ContentFrame.Navigate(typeof(ClonePage)); break;
                case "Wallet": ContentFrame.Navigate(typeof(WalletPage)); break;
                case "Mail": ContentFrame.Navigate(typeof(MailPage)); break;
                case "Contract": ContentFrame.Navigate(typeof(ContractPage)); break;
            }
        }
    }
}
