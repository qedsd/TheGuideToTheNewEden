using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.WinUI.Services;
using CommunityToolkit.Labs.WinUI;
using TheGuideToTheNewEden.WinUI.ViewModels;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Collections.ObjectModel;
using TheGuideToTheNewEden.WinUI.Models;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class CharactersShellPage : Page
    {
        public CharactersShellPage()
        {
            this.InitializeComponent();
            
            ClientServiceHelper.GetRequiredService<CharacterNavigationService>().Init(typeof(CharactersPage), typeof(CharacterPage), CharactersTabView);
            ClientServiceHelper.GetRequiredService<CharacterNavigationService>().NavigateToHome();
        }

        private void CharactersTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            var vm = (args.Item as TabViewItem).Tag;
            if (vm != null)
            {
                ClientServiceHelper.GetRequiredService<CharacterNavigationService>().RemoveInstance(vm);
            }
        }
    }
}
