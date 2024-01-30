using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WinUI.ViewModels.KB;
using Windows.Foundation;
using Windows.Foundation.Collections;
using ZKB.NET.Models.KillStream;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WinUI.Extensions;

namespace TheGuideToTheNewEden.WinUI.Views.KB
{
    public sealed partial class KBDetailPage : Page
    {
        private KBNavigationService _navigationService;
        private KBItemInfo _kbInfo;
        public KBDetailPage(KBItemInfo kbInfo, KBNavigationService navigationService)
        {
            _navigationService = navigationService;
            this.InitializeComponent();
            _kbInfo = kbInfo;
            VM.SetData(kbInfo);
            SetVictimInfo();
        }
        private void SetVictimInfo()
        {
            if (_kbInfo.SKBDetail.Victim.CharacterId != 0)
            {
                Image_Victim.Source = Converters.GameImageConverter.GetImageUri(_kbInfo.SKBDetail.Victim.CharacterId, Converters.GameImageConverter.ImgType.Character, 128);
                Button_Character.Content = _kbInfo.VictimCharacterName?.Name;
            }
            else
            {
                Image_Victim.Source = Converters.GameImageConverter.GetImageUri(_kbInfo.SKBDetail.Victim.CorporationId, Converters.GameImageConverter.ImgType.Corporation, 128);
                Button_Character.Visibility = Visibility.Collapsed;
            }
            if (_kbInfo.SKBDetail.Victim.CorporationId != 0)
            {
                Image_Corp.Source = Converters.GameImageConverter.GetImageUri(_kbInfo.SKBDetail.Victim.CorporationId, Converters.GameImageConverter.ImgType.Corporation, 64);
                Button_Corp.Content = _kbInfo.VictimCorporationIdName?.Name;
            }
            else
            {
                Image_Corp.Visibility = Visibility.Collapsed;
                Button_Corp.Visibility = Visibility.Collapsed;
            }
            if (_kbInfo.SKBDetail.Victim.AllianceId != 0)
            {
                Image_Alliance.Source = Converters.GameImageConverter.GetImageUri(_kbInfo.SKBDetail.Victim.AllianceId, Converters.GameImageConverter.ImgType.Alliance, 64);
                Button_Alliance.Content = _kbInfo.VictimAllianceName?.Name;
            }
            else
            {
                Image_Alliance.Visibility = Visibility.Collapsed;
                Button_Alliance.Visibility = Visibility.Collapsed;
            }
        }
        private void Button_Corp_Click(object sender, RoutedEventArgs e)
        {
            Navigation(_kbInfo.VictimCorporationIdName);
        }

        private void Button_Character_Click(object sender, RoutedEventArgs e)
        {
            Navigation(_kbInfo.VictimCharacterName);
        }

        private void Button_Alliance_Click(object sender, RoutedEventArgs e)
        {
            Navigation(_kbInfo.VictimAllianceName);
        }

        private void Button_Type_Click(object sender, RoutedEventArgs e)
        {
            Navigation(new IdName()
            {
                Id = VM.KBItemInfo.Type.TypeID,
                Name = VM.KBItemInfo.Type.TypeName,
                Category = (int)IdName.CategoryEnum.InventoryType
            });
        }

        private void Button_Group_Click(object sender, RoutedEventArgs e)
        {
            Navigation(new IdName()
            {
                Id = VM.KBItemInfo.Group.GroupID,
                Name = VM.KBItemInfo.Group.GroupName,
                Category = (int)IdName.CategoryEnum.Group
            });
        }

        private void Button_System_Click(object sender, RoutedEventArgs e)
        {
            Navigation(new IdName()
            {
                Id = VM.KBItemInfo.SolarSystem.SolarSystemID,
                Name = VM.KBItemInfo.SolarSystem.SolarSystemName,
                Category = (int)IdName.CategoryEnum.SolarSystem
            });
        }

        private void Button_Region_Click(object sender, RoutedEventArgs e)
        {
            Navigation(new IdName()
            {
                Id = VM.KBItemInfo.Region.RegionID,
                Name = VM.KBItemInfo.Region.RegionName,
                Category = (int)IdName.CategoryEnum.Region
            });
        }

        private void KBDamageControl_IdNameClicked(IdName idName)
        {
            Navigation(idName);
        }
        private async void Navigation(IdName idName)
        {
            this.GetBaseWindow()?.ShowWaiting();
            await _navigationService.NavigationTo(idName);
            this.GetBaseWindow()?.HideWaiting();
        }
    }
    public class CargoItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate GroupTemplate { get; set; }

        public DataTemplate SingleTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            var cargoItem = item as CargoItemInfo;
            if (cargoItem.SubItems.NotNullOrEmpty())
            {
                return GroupTemplate;
            }
            else
            {
                return SingleTemplate;
            }
        }
    }
}
