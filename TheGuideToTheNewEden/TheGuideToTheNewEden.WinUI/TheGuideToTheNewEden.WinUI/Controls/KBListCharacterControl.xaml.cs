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
using CommunityToolkit.WinUI.UI.Controls;
using TheGuideToTheNewEden.Core.Models.KB;
using ESI.NET.Models.Character;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class KBListCharacterControl : UserControl
    {
        public KBListCharacterControl()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty IsVictimProperty
           = DependencyProperty.Register(
               nameof(IsVictim),
               typeof(bool),
               typeof(KBListCharacterControl),
               new PropertyMetadata(null, null));

        public bool IsVictim
        {
            get => (bool)GetValue(IsVictimProperty);
            set
            {
                SetValue(IsVictimProperty, value);
            }
        }

        public static readonly DependencyProperty KBItemInfoProperty
           = DependencyProperty.Register(
               nameof(KBItemInfo),
               typeof(KBItemInfo),
               typeof(KBListCharacterControl),
               new PropertyMetadata(null, new PropertyChangedCallback(KBItemInfoPropertyChanged)));

        public KBItemInfo KBItemInfo
        {
            get => (KBItemInfo)GetValue(KBItemInfoProperty);
            set
            {
                SetValue(KBItemInfoProperty, value);
                if(IsVictim)
                {
                    if(value.SKBDetail.Victim.CharacterId != 0)//受害者是玩家
                    {
                        Image_Character.Source = Converters.GameImageConverter.GetImageUri(value.SKBDetail.Victim.CharacterId, Converters.GameImageConverter.ImgType.Character, 64);
                        TextBlock_Character.Text = value.VictimCharacterName?.Name;
                        if(value.SKBDetail.Victim.AllianceId != 0)//显示受害者联盟
                        {
                            Image_Faction.Source = Converters.GameImageConverter.GetImageUri(value.SKBDetail.Victim.AllianceId, Converters.GameImageConverter.ImgType.Alliance, 64);
                            TextBlock_Faction.Text = value.VictimAllianceName?.Name;
                        }
                        else//显示受害者军团
                        {
                            Image_Faction.Source = Converters.GameImageConverter.GetImageUri(value.SKBDetail.Victim.CorporationId, Converters.GameImageConverter.ImgType.Corporation, 64);
                            TextBlock_Faction.Text = value.VictimCorporationIdName?.Name;
                        }
                    }
                    else if(value.SKBDetail.Victim.CorporationId != 0)//受害者是军团
                    {
                        Image_Character.Source = Converters.GameImageConverter.GetImageUri(value.SKBDetail.Victim.CorporationId, Converters.GameImageConverter.ImgType.Corporation, 64);
                        TextBlock_Character.Text = value.VictimCorporationIdName?.Name;
                        if (value.SKBDetail.Victim.AllianceId != 0)//显示受害者联盟
                        {
                            Image_Faction.Source = Converters.GameImageConverter.GetImageUri(value.SKBDetail.Victim.AllianceId, Converters.GameImageConverter.ImgType.Alliance, 64);
                            TextBlock_Faction.Text = value.VictimAllianceName?.Name;
                        }
                        else
                        {
                            Image_Faction.Visibility = Visibility.Collapsed;
                        }
                    }
                    else if(value.SKBDetail.Victim.AllianceId != 0)//受害者是联盟
                    {
                        Image_Character.Source = Converters.GameImageConverter.GetImageUri(value.SKBDetail.Victim.AllianceId, Converters.GameImageConverter.ImgType.Alliance, 64);
                        TextBlock_Character.Text = value.VictimAllianceName?.Name;
                        Image_Faction.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    var finalBlow = value.GetFinalBlow();
                    if(finalBlow != null)
                    {
                        if (finalBlow.CharacterId != 0)//最后一击是玩家
                        {
                            Image_Character.Source = Converters.GameImageConverter.GetImageUri(finalBlow.CharacterId, Converters.GameImageConverter.ImgType.Character, 64);
                            TextBlock_Character.Text = value.FinalBlowCharacterName?.Name;
                            if (finalBlow.AllianceId != 0)//显示最后一击联盟
                            {
                                Image_Faction.Source = Converters.GameImageConverter.GetImageUri(finalBlow.AllianceId, Converters.GameImageConverter.ImgType.Alliance, 64);
                                TextBlock_Faction.Text = value.FinalBlowAllianceName?.Name;
                            }
                            else//显示最后一击军团
                            {
                                Image_Faction.Source = Converters.GameImageConverter.GetImageUri(finalBlow.CorporationId, Converters.GameImageConverter.ImgType.Corporation, 64);
                                TextBlock_Faction.Text = value.FinalBlowCorporationIdName?.Name;
                            }
                        }
                        else if (value.SKBDetail.Victim.CorporationId != 0)//最后一击是军团
                        {
                            Image_Character.Source = Converters.GameImageConverter.GetImageUri(finalBlow.CorporationId, Converters.GameImageConverter.ImgType.Corporation, 64);
                            TextBlock_Character.Text = value.FinalBlowCorporationIdName?.Name;
                            if (finalBlow.AllianceId != 0)//显示最后一击联盟
                            {
                                Image_Faction.Source = Converters.GameImageConverter.GetImageUri(finalBlow.AllianceId, Converters.GameImageConverter.ImgType.Alliance, 64);
                                TextBlock_Faction.Text = value.FinalBlowAllianceName?.Name;
                            }
                            else
                            {
                                Image_Faction.Visibility = Visibility.Collapsed;
                            }
                        }
                        else if (finalBlow.AllianceId != 0)//最后一击是联盟
                        {
                            Image_Character.Source = Converters.GameImageConverter.GetImageUri(finalBlow.AllianceId, Converters.GameImageConverter.ImgType.Alliance, 64);
                            TextBlock_Character.Text = value.FinalBlowAllianceName?.Name;
                            Image_Faction.Visibility = Visibility.Collapsed;
                        }
                    }
                    
                }
            }
        }
        private static void KBItemInfoPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

        }
        private void Button_Character_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
