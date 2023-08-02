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
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.DBModels;
using System.Text;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class WormholePage : Page
    {
        internal class SerachItem
        {
            public string Name {get;set;}
            public int Id { get; set; }
            public bool IsPortal
            {
                get => Obj.GetType() == typeof(WormholePortal);
            }
            public object Obj { get; set; }
        }
        public WormholePage()
        {
            this.InitializeComponent();
        }
        private SerachItem _selectedItem;
        private async void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if(string.IsNullOrEmpty(sender.Text))
            {
                sender.ItemsSource = null;
                return;
            }
            if(_selectedItem != null && _selectedItem.Name == sender.Text)
            {
                return;
            }
            List<SerachItem> serachItems = new List<SerachItem>();
            var holes = await Core.Services.DB.WormholeService.QueryWormholeAsync(sender.Text);
            var portals = await Core.Services.DB.WormholeService.QueryPortalAsync(sender.Text);
            if(holes.NotNullOrEmpty())
            {
                foreach( var hole in holes)
                {
                    serachItems.Add(new SerachItem()
                    {
                        Id = hole.Id,
                        Name = hole.Name,
                        Obj = hole
                    });
                }
            }
            if(portals.NotNullOrEmpty())
            {
                foreach (var portal in portals)
                {
                    serachItems.Add(new SerachItem()
                    {
                        Id = portal.Id,
                        Name = portal.Name,
                        Obj = portal
                    });
                }
            }
            sender.ItemsSource = serachItems;
        }

        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            _selectedItem = args.SelectedItem as SerachItem;
            if(_selectedItem != null)
            {
                if(_selectedItem.IsPortal)
                {
                    LoadWormholePortalDetail(_selectedItem.Obj as WormholePortal);
                }
                else
                {
                    LoadWormholeDetail(_selectedItem.Obj as Wormhole);
                }
            }
            sender.Text = _selectedItem.Name;
        }
        private void LoadWormholeDetail(Wormhole wormhole)
        {
            WormholePortalGrid.Visibility = Visibility.Collapsed;
            WormholeGrid.Visibility = Visibility.Visible;
            TextBlock_WhName.Text = wormhole.Name;
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(wormhole.Class);
            if(wormhole.Class > 6)
            {
                stringBuilder.Append('(');
                stringBuilder.Append(Helpers.ResourcesHelper.GetString($"WormholePage_Class_{wormhole.Class}"));
                stringBuilder.Append(')');
            }
            TextBlock_WhClass.Text = stringBuilder.ToString();
            StackPanel_Static.Children.Clear();
            if(!string.IsNullOrEmpty(wormhole.Statics))
            {
                foreach(var s in wormhole.Statics.Split(','))
                {
                    StackPanel_Static.Children.Add(CreatePortalButton(int.Parse(s)));
                }
            }
            StackPanel_Wandering.Children.Clear();
            if (!string.IsNullOrEmpty(wormhole.Wanderings))
            {
                foreach (var s in wormhole.Wanderings.Split(','))
                {
                    StackPanel_Wandering.Children.Add(CreatePortalButton(int.Parse(s)));
                }
            }
            if(wormhole.Phenomena < 0)
            {
                TextBlock_Phenomena.Text = string.Empty;
            }
            else
            {
                TextBlock_Phenomena.Text = Helpers.ResourcesHelper.GetString($"WormholePage_Phenomena_{wormhole.Phenomena}");
            }
            
        }
        private Button CreatePortalButton(int id)
        {
            var portal = WormholeService.QueryPortal(id);
            if(portal != null)
            {
                Button button = new Button()
                {
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    Tag = portal,
                    Margin = new Thickness(8,0,8,0),
                };
                if(string.IsNullOrEmpty(portal.Destination))
                {
                    button.Content = portal.Name;
                }
                else
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.Append(portal.Name);
                    stringBuilder.Append('(');
                    stringBuilder.Append(Helpers.ResourcesHelper.GetString($"WormholePage_Class_{portal.Destination}"));
                    stringBuilder.Append(')');
                    button.Content = stringBuilder.ToString();
                }
                button.Click += PortalButton_Click;
                return button;
            }
            else
            {
                return null;
            }
        }

        private void PortalButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

        }

        private void LoadWormholePortalDetail(WormholePortal wormholePortal)
        {
            WormholeGrid.Visibility = Visibility.Collapsed;
            WormholePortalGrid.Visibility = Visibility.Visible;
            WormholePortalPage.SetWormholePortal(wormholePortal);
        }
    }
}
