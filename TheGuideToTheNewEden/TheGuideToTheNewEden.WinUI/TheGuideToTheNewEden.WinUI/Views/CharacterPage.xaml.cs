﻿using Microsoft.UI.Xaml;
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
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.ViewModels;
using TheGuideToTheNewEden.WinUI.Views.Character;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class CharacterPage : Page
    {
        internal CharacterViewModel VM { get; private set; }
        internal CharacterPage(CharacterViewModel vm)
        {
            VM = vm;
            DataContext = VM;
            this.InitializeComponent();
            Loaded += CharacterPage_Loaded2;
        }
        private void CharacterPage_Loaded2(object sender, RoutedEventArgs e)
        {
            VM.Window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
        }

        private readonly Dictionary<string, Page> _contentPages = new Dictionary<string, Page>();
        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if(args.SelectedItem != null)
            {
                string tag = (args.SelectedItem as NavigationViewItem).Tag as string;
                switch (tag)
                {
                    case "Overview":
                        {
                            ContentFrame.Navigate(typeof(OverviewPage), new object[] { VM.EsiClient, VM.SelectedCharacter });
                        }
                        break;
                    case "Skill":
                        {
                            ContentFrame.Navigate(typeof(SkillPage), VM.Skill);
                        }
                        break;
                    case "Clone":
                        {
                            ContentFrame.Navigate(typeof(ClonePage), VM.EsiClient);
                        }
                        break;
                    case "Wallet": ContentFrame.Navigate(typeof(WalletPage), VM.EsiClient); break;
                    case "Mail": ContentFrame.Navigate(typeof(MailPage), VM.EsiClient); break;
                    case "Contract": ContentFrame.Navigate(typeof(Character.ContractPage), VM.EsiClient); break;
                    case "Industry": ContentFrame.Navigate(typeof(Character.IndustryPage), new dynamic[] { VM.EsiClient, VM.SelectedCharacter.CharacterID}); break;
                }
                if(_contentPages.ContainsKey(tag))
                {
                    _contentPages.Remove(tag);
                }
                _contentPages.Add(tag, ContentFrame.Content as Page);
            }
        }

        private void ImageBrush_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Log.Error(e.ErrorMessage);
        }

        private void ImageEx_CharacterAvatar_ImageExFailed(object sender, CommunityToolkit.WinUI.UI.Controls.ImageExFailedEventArgs e)
        {
            Log.Error(e.ErrorMessage);
        }

        private void Button_Refresh_Click(object sender, RoutedEventArgs e)
        {
            ResetPage();
            VM.RefreshCommand.Execute(null);
        }
        private void ResetPage()
        {
            foreach (var item in _contentPages.Values)
            {
                (item as ICharacterPage)?.Clear();
            }
            _contentPages.Clear();
            if(OverviewNavigationViewItem.IsSelected)
            {
                (ContentFrame.Content as ICharacterPage).Refresh();
            }
            else
            {
                OverviewNavigationViewItem.IsSelected = true;
            }
        }

        private void RefreshPageButton_Click(object sender, RoutedEventArgs e)
        {
            (ContentFrame.Content as ICharacterPage)?.Refresh();
        }

        private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            
        }
    }
}
