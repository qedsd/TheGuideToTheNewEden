using CommunityToolkit.Mvvm.Input;
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
                SetProperty(ref _selectedCharacter, value);
                Services.CharacterService.SetCurrentCharacter(value);
                if (_selectedCharacter != null)
                {
                    if(Core.Config.DefaultGameServer == Core.Enums.GameServerType.Tranquility)
                    {
                        var uri = new System.Uri($"https://imageserver.eveonline.com/Character/{value.CharacterID}_{512}.jpg");
                        CharacterAvatar = new BitmapImage(uri);
                    }
                    else
                    {
                        CharacterAvatar = null;
                    }
                    GetBaseInfoAsync(value);
                }
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

        #endregion
        public CharacterViewModel()
        {
            Characters = Services.CharacterService.CharacterOauths;
        }
        public void Init()
        {
            SelectedCharacter = Characters.FirstOrDefault();
        }
        public ICommand AddCommand => new RelayCommand(async() =>
        {
            AuthHelper.RegistyProtocol();
            Window.ShowWaiting("等待网页授权...");
            var result = await Services.CharacterService.AddAsync();
            if(result != null)
            {
                Window.ShowSuccess("添加成功");
                SelectedCharacter = result;
            }
            else
            {
                Window.ShowError("添加失败");
            }
            Window.HideWaiting();
        });
        public ICommand RefreshCommand => new RelayCommand(() =>
        {
            GetBaseInfoAsync(SelectedCharacter);
        });
        private async void GetBaseInfoAsync(AuthorizedCharacterData characterData)
        {
            if (characterData == null)
            {
                return;
            }
            Window?.ShowWaiting();
            await Services.CharacterService.WaitRefreshToken();
            ESI.NET.Models.Character.Information information = null;
            ESI.NET.Models.Skills.SkillDetails skill = null;
            List<ESI.NET.Models.Loyalty.Points> loyalties = null;
            
            decimal characterWallet = 0;
            List<ESI.NET.Models.Wallet.Wallet> corpWallets = null;
            var tasks = new Task[]
            {
                ESIService.Current.EsiClient.Character.Information(SelectedCharacter.CharacterID).ContinueWith((p)=>
                {
                    if(p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        information = p.Result.Data;
                    }
                    else
                    {
                        Log.Error(p?.Result.Message);
                    }
                }),
                ESIService.Current.EsiClient.Skills.List().ContinueWith((p)=>
                {
                    if(p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        skill = p.Result.Data;
                    }
                    else
                    {
                        Log.Error(p?.Result.Message);
                    }
                }),
                ESIService.Current.EsiClient.Loyalty.Points().ContinueWith((p)=>
                {
                    if(p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        loyalties = p.Result.Data;
                    }
                    else
                    {
                        Log.Error(p?.Result.Message);
                    }
                }),
                
                ESIService.Current.EsiClient.Wallet.CharacterWallet().ContinueWith((p)=>
                {
                    if(p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        characterWallet = p.Result.Data;
                    }
                    else
                    {
                        Log.Error(p?.Result.Message);
                    }
                }),
                ESIService.Current.EsiClient.Wallet.CorporationWallets().ContinueWith((p)=>
                {
                    if(p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        corpWallets = p.Result.Data;
                    }
                    else
                    {
                        Log.Error(p?.Result.Message);
                    }
                }),
            };
            await Task.WhenAll(tasks);
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
            Window?.HideWaiting();
        }
    }
}
