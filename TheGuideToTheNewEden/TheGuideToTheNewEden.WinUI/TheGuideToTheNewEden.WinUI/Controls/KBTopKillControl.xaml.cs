using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WinUI.Converters;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class KBTopKillControl : UserControl
    {
        public KBTopKillControl()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty KBItemInfoProperty
          = DependencyProperty.Register(
              nameof(KBItemInfo),
              typeof(KBItemInfo),
              typeof(KBTopKillControl),
              new PropertyMetadata(null, new PropertyChangedCallback(KBItemInfoPropertyPropertyChanged)));

        public KBItemInfo KBItemInfo
        {
            get => (KBItemInfo)GetValue(KBItemInfoProperty);
            set
            {
                SetValue(KBItemInfoProperty, value);
            }
        }

        private static void KBItemInfoPropertyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as KBTopKillControl;
            var value = e.NewValue as KBItemInfo;
            control.Image_Avatar.Source = Converters.GameImageConverter.GetImageUri(value.SKBDetail.Victim.ShipTypeId, Converters.GameImageConverter.ImgType.Type, 64);
            string victim = string.Empty;
            if(value.VictimCharacterName!= null )
            {
                victim = value.VictimCharacterName.Name;
            }
            else if(value.VictimCorporationIdName != null )
            {
                victim = value.VictimCorporationIdName.Name;
            }
            else if (value.VictimAllianceName != null)
            {
                victim = value.VictimAllianceName.Name;
            }
            control.Button_Victim.Content = victim;
            control.TextBlock_ISK.Text = ISKNormalizeConverter.Normalize(value.SKBDetail.Zkb.TotalValue);
            control.TextBlock_Ship.Text = value.Type.TypeName;
            //control.ImageBrush_Background.ImageSource = new BitmapImage(new Uri(Converters.GameImageConverter.GetImageUri(value.SKBDetail.Victim.ShipTypeId, Converters.GameImageConverter.ImgType.Type, 64)));
        }

        private void Button_Victim_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
