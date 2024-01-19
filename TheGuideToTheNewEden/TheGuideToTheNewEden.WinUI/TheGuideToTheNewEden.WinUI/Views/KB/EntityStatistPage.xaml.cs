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

namespace TheGuideToTheNewEden.WinUI.Views.KB
{
    public sealed partial class EntityStatistPage : Page
    {
        private EntityStatistic _statistic;
        public EntityStatistPage(EntityStatistic statistic)
        {
            _statistic = statistic;
            this.InitializeComponent();
        }

        private void SetBaseInfo()
        {
            switch(_statistic.Type)
            {
                case "characterID":; break;
                case "corporationID": break;
                case "allianceIDD": break;
                case "factionID": break;
                case "shipTypeID": break;
                case "groupID": break;
                case "solarSystemID": break;
                case "regionID": break;
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
    }
}
