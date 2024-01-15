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
using Windows.Foundation;
using Windows.Foundation.Collections;
using ZKB.NET.Models.Killmails;

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

        private static void AttackerPropertyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as KBDamageControl;
            var value = e.NewValue as AttackerInfo;
            if (value.Attacker.CharacterId != 0)
            {
                control.Image_Attacker.Source = Converters.GameImageConverter.GetImageUri(value.Attacker.CharacterId, Converters.GameImageConverter.ImgType.Character, 64);
                control.Button_Character.Content = value.CharacterName?.Name;
            }
            control.Image_Ship.Source = Converters.GameImageConverter.GetImageUri(value.Attacker.ShipTypeId, Converters.GameImageConverter.ImgType.Type, 32);
            control.Image_Weapon.Source = Converters.GameImageConverter.GetImageUri(value.Attacker.WeaponTypeId, Converters.GameImageConverter.ImgType.Type, 32);
            control.Button_Corp.Content = value.CorpName?.Name;
            if(value.Attacker.AllianceId == 0)
            {
                control.StackPanel_Alliance.Visibility = Visibility.Collapsed;
            }
            else
            {
                control.Button_Alliance.Content = value.AllianceName?.Name;
            }
            control.Button_Ship.Content = value.Ship?.TypeName;
            control.TextBlock_Weapon.Text = value.Weapon?.TypeName;
            if(value.Attacker.DamageDone == 0)
            {
                control.StackPanel_Damage.Visibility = Visibility.Collapsed;
            }
            else
            {
                control.TextBlock_Damage.Text = value.Attacker.DamageDone.ToString();
                control.TextBlock_DamageRatio.Text = (value.DamageRatio * 100).ToString("N2");
            }
        }
    }
}
