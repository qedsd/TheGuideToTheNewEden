﻿using CommunityToolkit.Mvvm.Input;
using ESI.NET.Logic;
using ESI.NET.Models.SSO;
using Microsoft.IdentityModel.Tokens;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Models.Mail;
using TheGuideToTheNewEden.Core.Models.Wallet;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.Core.Extensions;
using ESI.NET;
using Newtonsoft.Json.Linq;
using ESI.NET.Enumerations;
using Microsoft.Extensions.Options;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Dialogs;
using TheGuideToTheNewEden.WinUI.Converters;
using Vanara.PInvoke;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class CharacterViewModel: BaseViewModel
    {
        #region 属性
        private BitmapImage _characterAvatar;
        public BitmapImage CharacterAvatar { get => _characterAvatar; set => SetProperty(ref _characterAvatar, value); }

        private ObservableCollection<AuthorizedCharacterData> _characters;
        public ObservableCollection<AuthorizedCharacterData> Characters
        {
            get => _characters;
            set => SetProperty(ref _characters, value); 
        }
        private AuthorizedCharacterData _selectedCharacter;
        public AuthorizedCharacterData SelectedCharacter
        {
            get => _selectedCharacter;
            set
            {
                if(SetProperty(ref _selectedCharacter, value))
                    SetSelectedCharacter(value);
            }
        }

        private decimal _characterWallet;
        public decimal CharacterWallet { get => _characterWallet; set => SetProperty(ref _characterWallet, value); }

        private int _lp;
        public int LP { get => _lp; set => SetProperty(ref _lp, value); }
        private ESI.NET.Models.Character.Information _information;
        public ESI.NET.Models.Character.Information Information { get => _information; set => SetProperty(ref _information, value); }

        private ESI.NET.Models.Skills.SkillDetails _skill;
        public ESI.NET.Models.Skills.SkillDetails Skill { get => _skill; set => SetProperty(ref _skill, value); }

        private List<ESI.NET.Models.Loyalty.Points> _loyaltyPoints;
        public List<ESI.NET.Models.Loyalty.Points> LoyaltyPoints { get => _loyaltyPoints; set => SetProperty(ref _loyaltyPoints, value); }

        private ESI.NET.Models.Location.Activity _onlineStatus;
        public ESI.NET.Models.Location.Activity OnlineStatus { get => _onlineStatus; set => SetProperty(ref _onlineStatus, value); }

        private ESI.NET.Models.Location.Location _location;
        public ESI.NET.Models.Location.Location Location { get => _location; set => SetProperty(ref _location, value); }

        private ESI.NET.Models.Location.Ship _ship;
        public ESI.NET.Models.Location.Ship Ship { get => _ship; set => SetProperty(ref _ship, value); }

        private List<ESI.NET.Models.Wallet.Wallet> _corpWallets;
        public List<ESI.NET.Models.Wallet.Wallet> CorpWallets { get => _corpWallets; set => SetProperty(ref _corpWallets, value); }

        private decimal _corpWallet;
        public decimal CorpWallet { get => _corpWallet; set => SetProperty(ref _corpWallet, value); }

        private bool existedCharacter;
        public bool ExistedCharacter { get => existedCharacter; set => SetProperty(ref existedCharacter, value); }

        #endregion
        #region 字段
        public EsiClient EsiClient;
        #endregion
        public CharacterViewModel()
        {
            Characters = Services.CharacterService.CharacterOauths;
        }
        public void Init()
        {
            EsiClient = ESIService.GetDefaultEsi();
            SelectedCharacter = Characters.FirstOrDefault();
            if(!Characters.NotNullOrEmpty())
            {
                ExistedCharacter = false;
            }
            else
            {
                ExistedCharacter = true;
            }
        }
        public delegate void SelectedCharacterDelegate();
        public event SelectedCharacterDelegate OnSelectedCharacter;
        private void SetSelectedCharacter(AuthorizedCharacterData characterData)
        {
            Services.CharacterService.SetCurrentCharacter(characterData);
            EsiClient.SetCharacterData(characterData);
            if (characterData != null)
            {
                var uri = new System.Uri(GameImageConverter.GetImageUri(characterData.CharacterID, GameImageConverter.ImgType.Character, 512));
                CharacterAvatar = new BitmapImage(uri);
                GetBaseInfoAsync(characterData);
            }
        }
        public ICommand AddCommand => new RelayCommand(async() =>
        {
            if (GameServerSelectorService.Value == Core.Enums.GameServerType.Tranquility)
            {
                if (!AuthHelper.RegistyProtocol())
                {
                    Window.ShowError(Helpers.ResourcesHelper.GetString("CharacterPage_RegistyProtocol"));
                    return;
                }
                var result = await AddTranquilityAuthDialog.ShowAsync(Window.Content.XamlRoot);
                if(result != null)
                {
                    Window.ShowSuccess(Helpers.ResourcesHelper.GetString("CharacterPage_AddSuccess"));
                    SelectedCharacter = result;
                    ExistedCharacter = true;
                }
            }
            else
            {
                var result = await AddSerenityAuthDialog.ShowAsync(Window.Content.XamlRoot);
                if (result != null)
                {
                    Window.ShowSuccess(Helpers.ResourcesHelper.GetString("CharacterPage_AddSuccess"));
                    SelectedCharacter = result;
                    ExistedCharacter = true;
                }
            }
            
        });
        
        public ICommand RefreshCommand => new RelayCommand(() =>
        {
            GetBaseInfoAsync(SelectedCharacter);
        });

        public ICommand RemoveCommand => new RelayCommand<AuthorizedCharacterData>((character) =>
        {
            Services.CharacterService.Remove(character);
            Window.ShowSuccess("已删除角色");
            if(SelectedCharacter == null)
            {
                SelectedCharacter = Characters.FirstOrDefault();
                if(SelectedCharacter == null)
                {
                    ExistedCharacter = false;
                }
            }
        });
        private async void GetBaseInfoAsync(AuthorizedCharacterData characterData)
        {
            if (characterData == null)
            {
                return;
            }
            Window?.ShowWaiting();
            if(!characterData.IsTokenValid())
            {
                if(!await characterData.RefreshTokenAsync())
                {
                    Window?.HideWaiting();
                    Window?.ShowError($"Token已过期，尝试刷新失败（{Core.Log.GetLastError()}）");
                    return;
                }
            }
            ESI.NET.Models.Character.Information information = null;
            ESI.NET.Models.Skills.SkillDetails skill = null;
            List<ESI.NET.Models.Loyalty.Points> loyalties = null;
            
            decimal characterWallet = 0;
            List<ESI.NET.Models.Wallet.Wallet> corpWallets = null;
            var tasks = new Task[]
            {
                EsiClient.Character.Information(characterData.CharacterID).ContinueWith((p)=>
                {
                    if(p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        information = p.Result.Data;
                    }
                    else
                    {
                        Core.Log.Error(p?.Result.Message);
                    }
                }),
                EsiClient.Skills.List().ContinueWith((p)=>
                {
                    if(p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        skill = p.Result.Data;
                    }
                    else
                    {
                        Core.Log.Error(p?.Result.Message);
                    }
                }),
                EsiClient.Loyalty.Points().ContinueWith((p)=>
                {
                    if(p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        loyalties = p.Result.Data;
                    }
                    else
                    {
                        Core.Log.Error(p?.Result.Message);
                    }
                }),
                EsiClient.Wallet.CharacterWallet().ContinueWith((p)=>
                {
                    if(p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        if(decimal.TryParse(p.Result.Message, out var result))
                        {
                            characterWallet = result;
                        }
                    }
                    else
                    {
                        Core.Log.Error(p?.Result.Message);
                    }
                }),
                EsiClient.Wallet.CorporationWallets().ContinueWith((p)=>
                {
                    if(p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        corpWallets = p.Result.Data;
                    }
                    else
                    {
                        Core.Log.Error(p?.Result.Message);
                    }
                }),
            };
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                Window?.ShowError(ex.Message);
            }
            Window.DispatcherQueue.TryEnqueue(() =>
            {
                Skill = skill;
                LoyaltyPoints = loyalties;
                CharacterWallet = characterWallet;
                CorpWallets = corpWallets;
                Information = information;
                if(CorpWallets != null && CorpWallets.Any())
                {
                    CorpWallet = CorpWallets.Sum(p => p.Balance);
                }
                else
                {
                    CorpWallet = 0;
                }
                if(loyalties != null && loyalties.Any())
                {
                    LP = loyalties.Sum(p => p.LoyaltyPoints);
                }
                else
                {
                    LP = 0;
                }
            });
            OnSelectedCharacter?.Invoke();
            Window?.HideWaiting();
        }
    }
}
