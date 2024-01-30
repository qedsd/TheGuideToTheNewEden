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
using TheGuideToTheNewEden.Core.DBModels;
using static TheGuideToTheNewEden.Core.Events.IdNameEvent;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class KBListCharacterControl : UserControl
    {
        private IdName _characterIdName = new IdName();
        private IdName _factionIdName = new IdName();
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
                        _characterIdName.Id = value.SKBDetail.Victim.CharacterId;
                        _characterIdName.Category = (int)IdName.CategoryEnum.Character;
                        _characterIdName.Name = TextBlock_Character.Text;
                        if (value.SKBDetail.Victim.AllianceId != 0)//显示受害者联盟
                        {
                            Image_Faction.Source = Converters.GameImageConverter.GetImageUri(value.SKBDetail.Victim.AllianceId, Converters.GameImageConverter.ImgType.Alliance, 64);
                            TextBlock_Faction.Text = value.VictimAllianceName?.Name;
                            _factionIdName.Id = value.SKBDetail.Victim.AllianceId;
                            _factionIdName.Category = (int)IdName.CategoryEnum.Alliance;
                            _factionIdName.Name = TextBlock_Faction.Text;
                        }
                        else//显示受害者军团
                        {
                            Image_Faction.Source = Converters.GameImageConverter.GetImageUri(value.SKBDetail.Victim.CorporationId, Converters.GameImageConverter.ImgType.Corporation, 64);
                            TextBlock_Faction.Text = value.VictimCorporationIdName?.Name;
                            _factionIdName.Id = value.SKBDetail.Victim.CorporationId;
                            _factionIdName.Category = (int)IdName.CategoryEnum.Corporation;
                            _factionIdName.Name = TextBlock_Faction.Text;
                        }
                    }
                    else if(value.SKBDetail.Victim.CorporationId != 0)//受害者是军团
                    {
                        Image_Character.Source = Converters.GameImageConverter.GetImageUri(value.SKBDetail.Victim.CorporationId, Converters.GameImageConverter.ImgType.Corporation, 64);
                        TextBlock_Character.Text = value.VictimCorporationIdName?.Name;
                        _characterIdName.Id = value.SKBDetail.Victim.CorporationId;
                        _characterIdName.Category = (int)IdName.CategoryEnum.Corporation;
                        _characterIdName.Name = TextBlock_Character.Text;
                        if (value.SKBDetail.Victim.AllianceId != 0)//显示受害者联盟
                        {
                            Image_Faction.Source = Converters.GameImageConverter.GetImageUri(value.SKBDetail.Victim.AllianceId, Converters.GameImageConverter.ImgType.Alliance, 64);
                            TextBlock_Faction.Text = value.VictimAllianceName?.Name;
                            _factionIdName.Id = value.SKBDetail.Victim.AllianceId;
                            _factionIdName.Category = (int)IdName.CategoryEnum.Alliance;
                            _factionIdName.Name = TextBlock_Faction.Text;
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
                            _characterIdName.Id = finalBlow.CharacterId;
                            _characterIdName.Category = (int)IdName.CategoryEnum.Character;
                            _characterIdName.Name = TextBlock_Character.Text;
                            if (finalBlow.AllianceId != 0)//显示最后一击联盟
                            {
                                Image_Faction.Source = Converters.GameImageConverter.GetImageUri(finalBlow.AllianceId, Converters.GameImageConverter.ImgType.Alliance, 64);
                                TextBlock_Faction.Text = value.FinalBlowAllianceName?.Name;
                                _factionIdName.Id = finalBlow.AllianceId;
                                _factionIdName.Category = (int)IdName.CategoryEnum.Alliance;
                                _factionIdName.Name = TextBlock_Faction.Text;
                            }
                            else//显示最后一击军团
                            {
                                Image_Faction.Source = Converters.GameImageConverter.GetImageUri(finalBlow.CorporationId, Converters.GameImageConverter.ImgType.Corporation, 64);
                                TextBlock_Faction.Text = value.FinalBlowCorporationIdName?.Name;
                                _factionIdName.Id = finalBlow.CorporationId;
                                _factionIdName.Category = (int)IdName.CategoryEnum.Corporation;
                                _factionIdName.Name = TextBlock_Faction.Text;
                            }
                        }
                        else if (value.SKBDetail.Victim.CorporationId != 0)//最后一击是军团
                        {
                            Image_Character.Source = Converters.GameImageConverter.GetImageUri(finalBlow.CorporationId, Converters.GameImageConverter.ImgType.Corporation, 64);
                            TextBlock_Character.Text = value.FinalBlowCorporationIdName?.Name;
                            _characterIdName.Id = finalBlow.CorporationId;
                            _characterIdName.Category = (int)IdName.CategoryEnum.Corporation;
                            _characterIdName.Name = TextBlock_Character.Text;
                            if (finalBlow.AllianceId != 0)//显示最后一击联盟
                            {
                                Image_Faction.Source = Converters.GameImageConverter.GetImageUri(finalBlow.AllianceId, Converters.GameImageConverter.ImgType.Alliance, 64);
                                TextBlock_Faction.Text = value.FinalBlowAllianceName?.Name;
                                _factionIdName.Id = finalBlow.AllianceId;
                                _factionIdName.Category = (int)IdName.CategoryEnum.Alliance;
                                _factionIdName.Name = TextBlock_Faction.Text;
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
                            _characterIdName.Id = finalBlow.AllianceId;
                            _characterIdName.Category = (int)IdName.CategoryEnum.Alliance;
                            _characterIdName.Name = TextBlock_Character.Text;
                            Image_Faction.Visibility = Visibility.Collapsed;
                        }
                    }
                    TextBlock_AttackerCount.Text = value.SKBDetail.Attackers.Count.ToString();
                    StackPanel_AttackerCount.Visibility = Visibility.Visible;
                }
            }
        }
        private static void KBItemInfoPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

        }


        private IdNameClickedEventHandel _characterClicked;
        public event IdNameClickedEventHandel CharacterClicked
        {
            add
            {
                _characterClicked += value;
            }
            remove
            {
                _characterClicked -= value;
            }
        }

        private IdNameClickedEventHandel _factionClicked;
        public event IdNameClickedEventHandel FactionClicked
        {
            add
            {
                _factionClicked += value;
            }
            remove
            {
                _factionClicked -= value;
            }
        }
        private void Button_Character_Click(object sender, RoutedEventArgs e)
        {
            _characterClicked?.Invoke(_characterIdName);
        }

        private void Button_Faction_Click(object sender, RoutedEventArgs e)
        {
            _factionClicked?.Invoke(_factionIdName);
        }
    }
}
