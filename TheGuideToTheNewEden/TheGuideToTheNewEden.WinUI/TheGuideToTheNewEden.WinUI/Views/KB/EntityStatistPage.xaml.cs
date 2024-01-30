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
using ZKB.NET.Models.Statistics;
using TheGuideToTheNewEden.WinUI.Services;
using System.Text;
using static TheGuideToTheNewEden.WinUI.Converters.GameImageConverter;

namespace TheGuideToTheNewEden.WinUI.Views.KB
{
    public sealed partial class EntityStatistPage : Page
    {
        private readonly Services.KBNavigationService _kbNavigationService;
        private readonly EntityStatistic _statistic;
        public EntityStatistPage(EntityStatistic statistic, KBNavigationService kbNavigationService)
        {
            _kbNavigationService = kbNavigationService;
            _statistic = statistic;
            this.InitializeComponent();
            TabViewItem_List.Content = new StatistKBListPage(_statistic, _kbNavigationService);
            TabViewItem_TopValue.Content = new StatistTopValuePage(_statistic, _kbNavigationService);
            TabViewItem_TopAllTime.Content = new StatistTopAllTimePage(_statistic, _kbNavigationService);
            TabViewItem_Group.Content = new StatistGroupPage(_statistic, _kbNavigationService);
            TabViewItem_Month.Content = new StatistMonthPage(_statistic, _kbNavigationService);
            if(statistic.Supers == null)
            {
                TabView_Statist.TabItems.Remove(TabViewItem_Supper);
            }
            else
            {
                TabViewItem_Supper.Content = new StatistSuperPage(_statistic, _kbNavigationService);
            }
            Loaded += EntityStatistPage_Loaded;
        }

        private async void EntityStatistPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= EntityStatistPage_Loaded;
            VM.SetData(_statistic, _kbNavigationService);
            await VM.InitAsync();
            SetAvatar();
            SetSuper();
        }

        private void SetAvatar()
        {
            Converters.GameImageConverter.ImgType imgType;
            switch (_statistic.Type)
            {
                case "characterID":
                    {
                        imgType = Converters.GameImageConverter.ImgType.Character;
                    }
                    break;
                case "corporationID":
                    {
                        imgType = Converters.GameImageConverter.ImgType.Corporation;
                    }
                    break;
                case "allianceID":
                    {
                        imgType = Converters.GameImageConverter.ImgType.Alliance;
                    }
                    break;
                case "shipTypeID":
                    {
                        imgType = Converters.GameImageConverter.ImgType.Type;
                    }
                    break;
                case "groupID":
                case "solarSystemID":
                case "regionID":
                case "factionID":
                default:
                    {
                        Image_Avatar.Visibility = Visibility.Collapsed;
                    }
                    return;
            }
            Image_Avatar.Source = Converters.GameImageConverter.GetImageUri(_statistic.Id, imgType, 128);
        }
        private void SetSuper()
        {
            switch (_statistic.Type)
            {
                case "characterID":
                case "corporationID":
                case "allianceID":
                    {
                        if(_statistic.HasSupers)
                        {
                            TextBlock_HasSupers.Text = Helpers.ResourcesHelper.GetString("General_Yes");
                        }
                        else
                        {
                            TextBlock_HasSupers.Text = Helpers.ResourcesHelper.GetString("General_No");
                        }
                    }
                    break;
                default:
                    {
                        StackPanel_HasSupers.Visibility = Visibility.Collapsed;
                    }
                    return;
            }
        }

        private void Button_Character_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Corp_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Alliance_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Region_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Constellation_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Ship_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Class_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Sec_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Members_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_System_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Menu_OpenInZKB_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("https://zkillboard.com/");
            switch (_statistic.Type)
            {
                case "shipTypeID": stringBuilder.Append("ship");break;
                case "solarSystemID": stringBuilder.Append("system"); break;
                default:
                    {
                        stringBuilder.Append(_statistic.Type[..^2]);
                    }break;
            }
            
            stringBuilder.Append('/');
            stringBuilder.Append(_statistic.Id);
            stringBuilder.Append('/');
            Helpers.UrlHelper.OpenInBrower(stringBuilder.ToString());
        }
    }
}
