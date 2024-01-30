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
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using Windows.Foundation;
using Windows.Foundation.Collections;
using ZKB.NET.Models.Killmails;
using static TheGuideToTheNewEden.Core.Events.IdNameEvent;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class KBDamageControl : UserControl
    {
        public KBDamageControl()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty AttackerProperty
           = DependencyProperty.Register(
               nameof(Attacker),
               typeof(AttackerInfo),
               typeof(KBDamageControl),
               new PropertyMetadata(null, new PropertyChangedCallback(AttackerPropertyPropertyChanged)));

        public AttackerInfo Attacker
        {
            get => (AttackerInfo)GetValue(AttackerProperty);
            set
            {
                SetValue(AttackerProperty, value);
            }
        }

        private IdName _character;
        private IdName _corp;
        private IdName _alliance;
        private IdName _ship;
        private static void AttackerPropertyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as KBDamageControl;
            var value = e.NewValue as AttackerInfo;
            if (value.Attacker.CharacterId != 0)
            {
                control.Image_Attacker.Source = Converters.GameImageConverter.GetImageUri(value.Attacker.CharacterId, Converters.GameImageConverter.ImgType.Character, 64);
                control.Button_Character.Content = value.CharacterName?.Name;
                control._character = new IdName()
                {
                    Id = value.Attacker.CharacterId,
                    Name = value.CharacterName?.Name,
                    Category = (int)IdName.CategoryEnum.Character
                };
            }
            control.Image_Ship.Source = Converters.GameImageConverter.GetImageUri(value.Attacker.ShipTypeId, Converters.GameImageConverter.ImgType.Type, 32);
            control.Image_Weapon.Source = Converters.GameImageConverter.GetImageUri(value.Attacker.WeaponTypeId, Converters.GameImageConverter.ImgType.Type, 32);
            control.Button_Corp.Content = value.CorpName?.Name;
            control._corp = new IdName()
            {
                Id = value.Attacker.CorporationId,
                Name = value.CorpName?.Name,
                Category = (int)IdName.CategoryEnum.Corporation
            };
            if (value.Attacker.AllianceId == 0)
            {
                control.StackPanel_Alliance.Visibility = Visibility.Collapsed;
            }
            else
            {
                control.Button_Alliance.Content = value.AllianceName?.Name;
                control._alliance = new IdName()
                {
                    Id = value.Attacker.AllianceId,
                    Name = value.AllianceName?.Name,
                    Category = (int)IdName.CategoryEnum.Alliance
                };
            }
            control.Button_Ship.Content = value.Ship?.TypeName;
            control.TextBlock_Weapon.Text = value.Weapon?.TypeName;
            if(string.IsNullOrEmpty(control.TextBlock_Weapon.Text))
            {
                control.TextBlock_WeaponSplit.Visibility = Visibility.Collapsed;
            }
            if(value.Attacker.DamageDone == 0)
            {
                control.StackPanel_Damage.Visibility = Visibility.Collapsed;
            }
            else
            {
                control.TextBlock_Damage.Text = value.Attacker.DamageDone.ToString("N0");
                control.TextBlock_DamageRatio.Text = (value.DamageRatio * 100).ToString("N2");
            }
            control._ship = new IdName()
            {
                Id = value.Attacker.ShipTypeId,
                Name = value.Ship?.TypeName,
                Category = (int)IdName.CategoryEnum.InventoryType
            };
        }

        private void Button_Character_Click(object sender, RoutedEventArgs e)
        {
            _idNameClicked?.Invoke(_character);
        }

        private void Button_Corp_Click(object sender, RoutedEventArgs e)
        {
            _idNameClicked?.Invoke(_corp);
        }

        private void Button_Alliance_Click(object sender, RoutedEventArgs e)
        {
            _idNameClicked?.Invoke(_alliance);
        }

        private void Button_Ship_Click(object sender, RoutedEventArgs e)
        {
            _idNameClicked?.Invoke(_ship);
        }

        private IdNameClickedEventHandel _idNameClicked;
        public event IdNameClickedEventHandel IdNameClicked
        {
            add
            {
                _idNameClicked += value;
            }
            remove
            {
                _idNameClicked -= value;
            }
        }
    }
}
