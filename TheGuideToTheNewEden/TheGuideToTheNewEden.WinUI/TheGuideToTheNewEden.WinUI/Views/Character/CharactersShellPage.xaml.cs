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
            NavigationToken.Items.Add(new TokenItem()
            {
                Content = Helpers.ResourcesHelper.GetString("General_All"),
                IsSelected = true,
                Tag = null
            });
            NavigationToken.SelectionChanged += NavigationToken_SelectionChanged;
            
            ClientServiceHelper.GetRequiredService<CharacterNavigationService>().Navigated += CharactersShellPage_Navigated;
            ClientServiceHelper.GetRequiredService<CharacterNavigationService>().Removed += CharactersShellPage_Removed;
            ClientServiceHelper.GetRequiredService<CharacterNavigationService>().Init(typeof(CharactersPage), typeof(CharacterPage), ContentFrame);
            ClientServiceHelper.GetRequiredService<CharacterNavigationService>().NavigateToHome();
        }

        private void CharactersShellPage_Removed(object sender, object e)
        {
            var targetItem = NavigationToken.Items.FirstOrDefault(p => (p as TokenItem).Tag == e);
            if (targetItem != null)
            {
                NavigationToken.Items.Remove(targetItem);
            }
        }

        private void CharactersShellPage_Navigated(object sender, object e)
        {
            if(e == (NavigationToken.SelectedItem as TokenItem).Tag)
            {
                //由选择Token导致的切换，不做处理
            }
            else
            {
                var vm = e as CharacterViewModel;
                if (vm != null) 
                {
                    var targetItem = NavigationToken.Items.FirstOrDefault(p => (p as TokenItem).Tag == vm);
                    if(targetItem != null)
                    {
                        NavigationToken.SelectedItem = targetItem;
                    }
                    else
                    {
                        //所有角色页面选中角色导致的切换
                        TokenItem tokenItem = new TokenItem()
                        {
                            Content = vm.SelectedCharacter.CharacterName,
                            //Icon = new ImageIcon() 
                            //{ 
                            //    Source = new BitmapImage(new Uri(Converters.GameImageConverter.GetImageUri(vm.SelectedCharacter.CharacterID, Converters.GameImageConverter.ImgType.Character, 32))),
                            //},
                            IsSelected = true,
                            Tag = vm,
                            IsRemoveable = true
                        };
                        tokenItem.Removing += TokenItem_Removing;
                        NavigationToken.Items.Add(tokenItem);
                    }
                }
            }
        }

        private void TokenItem_Removing(object sender, TokenItemRemovingEventArgs e)
        {
            var vm = e.TokenItem.Tag as CharacterViewModel;
            if (vm != null)
            {
                ClientServiceHelper.GetRequiredService<CharacterNavigationService>().RemoveInstance(vm);
            }
            NavigationToken.SelectedIndex = 0;
        }

        /// <summary>
        /// 切换已打开的页面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NavigationToken_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = e.AddedItems.LastOrDefault() as TokenItem;
            if (item != null)
            {
                if(item.Tag == null)
                {
                    ClientServiceHelper.GetRequiredService<CharacterNavigationService>().NavigateToHome();
                }
                else
                {
                    ClientServiceHelper.GetRequiredService<CharacterNavigationService>().NavigateTo(item.Tag);
                }
            }
        }
    }
}
