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
using System.Windows.Input;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WinUI.Converters;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static TheGuideToTheNewEden.Core.Events.IdNameEvent;

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
            control.Button_Ship.Content = value.Type.TypeName;
            //control.ImageBrush_Background.ImageSource = new BitmapImage(new Uri(Converters.GameImageConverter.GetImageUri(value.SKBDetail.Victim.ShipTypeId, Converters.GameImageConverter.ImgType.Type, 64)));
        }

        private void Button_Victim_Click(object sender, RoutedEventArgs e)
        {
            _idNameClicked?.Invoke(KBItemInfo.Victim);
            IdNameClickedCommand?.Execute(KBItemInfo.Victim);
        }

        private void Button_Ship_Click(object sender, RoutedEventArgs e)
        {
            IdName idName = new IdName()
            {
                Id = KBItemInfo.Type.TypeID,
                Name = KBItemInfo.Type.TypeName,
                Category = (int)IdName.CategoryEnum.InventoryType
            };
            _idNameClicked?.Invoke(idName);
            IdNameClickedCommand?.Execute(idName);
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

        public static readonly DependencyProperty IdNameClickedCommandProperty
          = DependencyProperty.Register(
              nameof(IdNameClickedCommand),
              typeof(ICommand),
              typeof(KBTopKillControl),
              new PropertyMetadata(default(ICommand)));

        public ICommand IdNameClickedCommand
        {
            get => (ICommand)GetValue(IdNameClickedCommandProperty);
            set => SetValue(IdNameClickedCommandProperty, value);
        }
    }
}
