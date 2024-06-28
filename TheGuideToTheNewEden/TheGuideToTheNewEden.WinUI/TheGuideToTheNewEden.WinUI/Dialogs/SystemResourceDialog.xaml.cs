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

namespace TheGuideToTheNewEden.WinUI.Dialogs
{
    public sealed partial class SystemResourceDialog : Page
    {
        public SystemResourceDialog(MapSolarSystem system, MapRegion region, SovData sov, SolarSystemResources resources)
        {
            this.InitializeComponent();
            SystemIDTextBlock.Text = system.SolarSystemID.ToString();
            SystemSecurityTextBlock.Text = system.Security.ToString("N1");
            SystemRegionTextBlock.Text = region.RegionName;
            if(sov != null)
            {
                SystemSOVNameTextBlock.Text = sov.AllianceName;
                SystemSOVIDTextBlock.Text = sov.AllianceId.ToString();
            }
            PowerTextBlock.Text = resources.Power.ToString("N0");
            WorkforceTextBlock.Text = resources.Workforce.ToString("N0");
            MagmaticGasTextBlock.Text = resources.MagmaticGas.ToString("N0");
            SuperionicIceTextBlock.Text = resources.SuperionicIce.ToString("N0");

            var upgrades = UpgradeService.Current.GetUpgrades();
            List<UpgradeStatus> upgradeStatuses = new List<UpgradeStatus>();
            foreach(var upgrade in upgrades)
            {
                UpgradeStatus upgradeStatus = new UpgradeStatus();
                upgradeStatus.Upgrade = upgrade;
                upgradeStatus.Fit = upgrade.Power <= resources.Power;
                upgradeStatuses.Add(upgradeStatus);
            }
            UpgradeList.ItemsSource = upgradeStatuses;

            ResourceDetailList.ItemsSource = SolarSystemResourcesService.GetPlanetResourcesDetailsBySolarSystemID(system.SolarSystemID).Where(p=>p.ContainResource);
        }
        public static async Task ShowAsync(MapSolarSystem system, MapRegion region, SovData sov, SolarSystemResources resources, XamlRoot xamlRoot)
        {
            SystemResourceDialog content = new SystemResourceDialog(system, region, sov, resources);
            ContentDialog contentDialog = new ContentDialog()
            {
                XamlRoot = xamlRoot,
                Title = system.SolarSystemName,
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
    }
}
