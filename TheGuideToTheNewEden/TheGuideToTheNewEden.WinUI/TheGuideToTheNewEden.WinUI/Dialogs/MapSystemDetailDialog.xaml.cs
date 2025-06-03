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
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.PlanetResources;
using TheGuideToTheNewEden.Core.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static TheGuideToTheNewEden.WinUI.Controls.MapDataTypeControl;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.Core.Models.Map;

namespace TheGuideToTheNewEden.WinUI.Dialogs
{
    public class MapSystemDetailInfo
    {
        public MapSolarSystem System { get; set; }
        public MapRegion Region { get; set; }
        public SovData Sov { get; set; }
        public SolarSystemResources Resources { get; set; }
        public List<MapSystemInfo> JumpTos { get; set; }
        public int ShipKills { get; set; }
        public int NpcKills { get; set; }
        public int PodKills { get; set; }
        public int Jumps { get; set; }
    }
    public sealed partial class MapSystemDetailDialog : Page
    {
        public MapSystemDetailDialog(MapSystemDetailInfo mapSystemDetailInfo)
        {
            this.InitializeComponent();
            SystemIDTextBlock.Text = mapSystemDetailInfo.System.SolarSystemID.ToString();
            SystemSecurityTextBlock.Text = mapSystemDetailInfo.System.Security.ToString("N2");
            SystemRegionTextBlock.Text = mapSystemDetailInfo.Region.RegionName;
            if(mapSystemDetailInfo.Sov != null)
            {
                SystemSOVNameTextBlock.Text = mapSystemDetailInfo.Sov.AllianceName;
                SystemSOVIDTextBlock.Text = mapSystemDetailInfo.Sov.AllianceId.ToString();
            }
            PowerTextBlock.Text = mapSystemDetailInfo.Resources.Power.ToString("N0");
            WorkforceTextBlock.Text = mapSystemDetailInfo.Resources.Workforce.ToString("N0");
            MagmaticGasTextBlock.Text = mapSystemDetailInfo.Resources.MagmaticGas.ToString("N0");
            SuperionicIceTextBlock.Text = mapSystemDetailInfo.Resources.SuperionicIce.ToString("N0");

            var upgrades = UpgradeService.Current.GetUpgrades();
            List<UpgradeStatus> upgradeStatuses = new List<UpgradeStatus>();
            foreach(var upgrade in upgrades)
            {
                UpgradeStatus upgradeStatus = new UpgradeStatus();
                upgradeStatus.Upgrade = upgrade;
                upgradeStatus.Fit = upgrade.Power <= mapSystemDetailInfo.Resources.Power;
                upgradeStatuses.Add(upgradeStatus);
            }
            UpgradeList.ItemsSource = upgradeStatuses;

            ResourceDetailList.ItemsSource = SolarSystemResourcesService.GetPlanetResourcesDetailsBySolarSystemID(mapSystemDetailInfo.System.SolarSystemID).Where(p=>p.ContainResource);
            CelestialList.ItemsSource = MapDenormalizeService.QueryBySolarSystemID(mapSystemDetailInfo.System.SolarSystemID);
            ShipKillsTextBlock.Text = mapSystemDetailInfo.ShipKills.ToString();
            PodKillsTextBlock.Text = mapSystemDetailInfo.PodKills.ToString();
            NpcKillsTextBlock.Text = mapSystemDetailInfo.NpcKills.ToString();
            JumpsTextBlock.Text = mapSystemDetailInfo.Jumps.ToString();
            JumpTosList.ItemsSource = mapSystemDetailInfo.JumpTos;
        }
        public static async Task ShowAsync(MapSystemDetailInfo mapSystemDetailInfo,  XamlRoot xamlRoot)
        {
            MapSystemDetailDialog content = new MapSystemDetailDialog(mapSystemDetailInfo);
            ContentDialog contentDialog = new ContentDialog()
            {
                XamlRoot = xamlRoot,
                Title = mapSystemDetailInfo.System.SolarSystemName,
                Content = content,
                PrimaryButtonText = Helpers.ResourcesHelper.GetString("General_OK")
            };
            await contentDialog.ShowAsync();
        }

        public class UpgradeStatus
        {
            public Upgrade Upgrade { get; set; }
            public bool Fit { get; set; }
        }

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StatisticsGrid.Visibility = Visibility.Collapsed;
            UpgradeListGrid.Visibility = Visibility.Collapsed;
            ResourceDetailListGrid.Visibility = Visibility.Collapsed;
            JumpTosListGrid.Visibility = Visibility.Collapsed;
            CelestialListGrid.Visibility = Visibility.Collapsed;
            switch ((sender as Pivot).SelectedIndex)
            {
                case 0: StatisticsGrid.Visibility = Visibility.Visible;break;
                case 1: UpgradeListGrid.Visibility = Visibility.Visible; break;
                case 2: ResourceDetailListGrid.Visibility = Visibility.Visible; break;
                case 3: CelestialListGrid.Visibility = Visibility.Visible; break;
                case 4: JumpTosListGrid.Visibility = Visibility.Visible; break;
            }
        }
    }
}
