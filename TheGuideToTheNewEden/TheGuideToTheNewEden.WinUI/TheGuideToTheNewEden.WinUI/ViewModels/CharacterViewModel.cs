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
using TheGuideToTheNewEden.Core.Extensions;
using ESI.NET;
using Newtonsoft.Json.Linq;
using ESI.NET.Enumerations;
using Microsoft.Extensions.Options;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Dialogs;
using TheGuideToTheNewEden.WinUI.Converters;
using Vanara.PInvoke;
using ESI.NET.Models.Location;
using ESI.NET.Models.Universe;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class CharacterViewModel: BaseViewModel
    {
        private AuthorizedCharacterData _selectedCharacter;
        public AuthorizedCharacterData SelectedCharacter
        {
            get => _selectedCharacter;
            set
            {
                SetProperty(ref _selectedCharacter, value);
            }
        }

        #region
        private decimal _characterWallet;
        private int _lp;
        private ESI.NET.Models.Character.Information _information;
        private ESI.NET.Models.Skills.SkillDetails _skill;
        private List<ESI.NET.Models.Loyalty.Points> _loyaltyPoints;
        private ESI.NET.Models.Location.Activity _onlineStatus;
        private ESI.NET.Models.Location.Location _location;
        private ESI.NET.Models.Location.Ship _ship;
        private List<ESI.NET.Models.Wallet.Wallet> _corpWallets;
        private decimal _corpWallet;
        private string _offLineTime;
        private bool _skillQueueRunning;
        private string _skillQueueRemainRatio = "0";
        private string _skillQueueRemainTime;
        private int _skillQueueTotalCount;
        private int _skillQueueUndoneCount;
        private ESI.NET.Models.Corporation.Corporation _corporation = null;
        private ESI.NET.Models.Alliance.Alliance _alliance = null;
        #endregion

        #region 详细页属性
        private BitmapImage _characterAvatar;
        public BitmapImage CharacterAvatar { get => _characterAvatar; set => SetProperty(ref _characterAvatar, value); }
        public decimal CharacterWallet { get => _characterWallet; set => SetProperty(ref _characterWallet, value); }
        public int LP { get => _lp; set => SetProperty(ref _lp, value); }
        public ESI.NET.Models.Character.Information Information { get => _information; set => SetProperty(ref _information, value); }
        public ESI.NET.Models.Skills.SkillDetails Skill { get => _skill; set => SetProperty(ref _skill, value); }
        public List<ESI.NET.Models.Loyalty.Points> LoyaltyPoints { get => _loyaltyPoints; set => SetProperty(ref _loyaltyPoints, value); }
        public ESI.NET.Models.Location.Activity OnlineStatus { get => _onlineStatus; set => SetProperty(ref _onlineStatus, value); }
        public ESI.NET.Models.Location.Location Location { get => _location; set => SetProperty(ref _location, value); }
        public ESI.NET.Models.Location.Ship Ship { get => _ship; set => SetProperty(ref _ship, value); }
        public List<ESI.NET.Models.Wallet.Wallet> CorpWallets { get => _corpWallets; set => SetProperty(ref _corpWallets, value); }
        public decimal CorpWallet { get => _corpWallet; set => SetProperty(ref _corpWallet, value); }
        public ESI.NET.Models.Corporation.Corporation Corporation { get => _corporation; set => SetProperty(ref _corporation, value); }
        public ESI.NET.Models.Alliance.Alliance Alliance { get => _alliance; set => SetProperty(ref _alliance, value); }
        #endregion

        #region 卡片页属性
        private BitmapImage _characterAvatar_Card;
        public BitmapImage CharacterAvatar_Card { get => _characterAvatar_Card; set => SetProperty(ref _characterAvatar_Card, value); }
        public string CharacterWallet_Card { get => _characterWallet.ToString("N2"); }
        public int LP_Card { get => _lp; set => SetProperty(ref _lp, value); }
        public ESI.NET.Models.Character.Information Information_Card { get => _information; set => SetProperty(ref _information, value); }
        public string TotalSP { get => _skill?.TotalSp.ToString("N0"); }
        public ESI.NET.Models.Skills.SkillDetails Skill_Card { get => _skill; set => SetProperty(ref _skill, value); }
        public List<ESI.NET.Models.Loyalty.Points> LoyaltyPoints_Card { get => _loyaltyPoints; set => SetProperty(ref _loyaltyPoints, value); }
        public ESI.NET.Models.Location.Activity OnlineStatus_Card { get => _onlineStatus; set => SetProperty(ref _onlineStatus, value); }
        public ESI.NET.Models.Location.Location Location_Card { get => _location; set => SetProperty(ref _location, value); }
        public ESI.NET.Models.Location.Ship Ship_Card { get => _ship; set => SetProperty(ref _ship, value); }
        public List<ESI.NET.Models.Wallet.Wallet> CorpWallets_Card { get => _corpWallets; set => SetProperty(ref _corpWallets, value); }
        public decimal CorpWallet_Card { get => _corpWallet; set => SetProperty(ref _corpWallet, value); }
        public string OffLineTime { get => _offLineTime; set => SetProperty(ref _offLineTime, value); }
        public bool SkillQueueRunning { get => _skillQueueRunning; set => SetProperty(ref _skillQueueRunning, value); }
        public string SkillQueueRemainTime { get => _skillQueueRemainTime; set => SetProperty(ref _skillQueueRemainTime, value); }
        public int SkillQueueTotalCount { get => _skillQueueTotalCount; set => SetProperty(ref _skillQueueTotalCount, value); }
        public int SkillQueueUndoneCount { get => _skillQueueUndoneCount; set => SetProperty(ref _skillQueueUndoneCount, value); }
        public string SkillQueueRemainRatio { get => _skillQueueRemainRatio; set => SetProperty(ref _skillQueueRemainRatio, value); }
        #endregion

        #region ZKB
        private bool _hasZKB;
        public bool HasZKB { get => _hasZKB; set => SetProperty(ref _hasZKB, value); }

        private int _itemLost;
        public int ItemLost { get => _itemLost; set => SetProperty(ref _itemLost, value); }

        private int _itemDestroyed;
        public int ItemDestroyed { get => _itemDestroyed; set => SetProperty(ref _itemDestroyed, value); }

        private long _iskLost;
        public long ISKLost { get => _iskLost; set => SetProperty(ref _iskLost, value); }

        private long _iskDestroyed;
        public long ISKDestroyed { get => _iskDestroyed; set => SetProperty(ref _iskDestroyed, value); }

        public int _soloKills;
        public int SoloKills { get => _soloKills; set => SetProperty(ref _soloKills, value); }

        private int _dangerRatio;
        public int DangerRatio { get => _dangerRatio; set => SetProperty(ref _dangerRatio, value); }

        private int gangRatio;
        public int GangRatio { get => gangRatio; set => SetProperty(ref gangRatio, value); }
        #endregion

        public EsiClient EsiClient;
        public CharacterViewModel()
        {
            
        }
        public CharacterViewModel(AuthorizedCharacterData characterData)
        {
            SelectedCharacter = characterData;
        }
        public void Init()
        {
            EsiClient = ESIService.GetDefaultEsi();
            EsiClient.SetCharacterData(SelectedCharacter);
            GetBaseInfoAsync().Wait();
        }
        public ICommand RefreshCommand => new RelayCommand(async() =>
        {
            Window?.ShowWaiting();
            await GetBaseInfoAsync();
            await GetZKBInfoAsync();
            Window?.HideWaiting();
        });
        public ICommand ZKBCommand => new RelayCommand(async () =>
        {
            await KBNavigationService.Default.NavigationTo(SelectedCharacter.CharacterID, ZKB.NET.EntityType.CharacterID, SelectedCharacter.CharacterName);
        });
        private async Task GetBaseInfoAsync()
        {
            var characterData = SelectedCharacter;
            if (!characterData.IsTokenValid())
            {
                if(!await characterData.RefreshTokenAsync())
                {
                    Window?.HideWaiting();
                    Window?.ShowError($"{SelectedCharacter.CharacterName}: {Helpers.ResourcesHelper.GetString("CharacterPage_TryUpdateTokenFailed")}（{Core.Log.GetLastError()}）");
                    return;
                }
            }
            ESI.NET.Models.Character.Information information = null;
            ESI.NET.Models.Skills.SkillDetails skill = null;
            List<ESI.NET.Models.Loyalty.Points> loyalties = null;
            
            decimal characterWallet = 0;
            List<ESI.NET.Models.Wallet.Wallet> corpWallets = null;
            List<ESI.NET.Models.Skills.SkillQueueItem> skillQueueItems = null;
            ESI.NET.Models.Location.Activity onlineStatus = null;

            ESI.NET.Models.Corporation.Corporation corporation = null;
            ESI.NET.Models.Alliance.Alliance alliance = null;
            var tasks = new List<Task>()
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
                EsiClient.Skills.Queue().ContinueWith((p) =>
                {
                    if (p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        skillQueueItems = p.Result.Data;
                    }
                    else
                    {
                        Core.Log.Error(p?.Result.Message);
                    }
                }),
                EsiClient.Location.Online().ContinueWith((p)=>
                {
                    if(p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        onlineStatus = p.Result.Data;
                    }
                    else
                    {
                        Core.Log.Error(p?.Result.Message);
                    }
                }),
                EsiClient.Corporation.Information(characterData.CorporationID).ContinueWith((p) =>
                {
                    if (p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        corporation = p.Result.Data;
                    }
                    else
                    {
                        Core.Log.Error(p?.Result.Message);
                    }
                })
            };
            if (characterData.AllianceID > 0)
            {
                tasks.Add(EsiClient.Alliance.Information(characterData.AllianceID).ContinueWith((p) =>
                {
                    if (p?.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        alliance = p.Result.Data;
                    }
                    else
                    {
                        Core.Log.Error(p?.Result.Message);
                    }
                }));
            }
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
                _characterAvatar = new BitmapImage(new System.Uri(GameImageConverter.GetImageUri(SelectedCharacter.CharacterID, GameImageConverter.ImgType.Character, 512)));
                _characterAvatar_Card = new BitmapImage(new System.Uri(GameImageConverter.GetImageUri(SelectedCharacter.CharacterID, GameImageConverter.ImgType.Character, 128)));
                _skill = skill;
                _loyaltyPoints = loyalties;
                _characterWallet = characterWallet;
                _corpWallets = corpWallets;
                _information = information;
                if (_corpWallets != null && _corpWallets.Any())
                {
                    _corpWallet = _corpWallets.Sum(p => p.Balance);
                }
                else
                {
                    _corpWallet = 0;
                }
                if (loyalties != null && loyalties.Any())
                {
                    _lp = loyalties.Sum(p => p.LoyaltyPoints);
                }
                else
                {
                    _lp = 0;
                }
                _onlineStatus = onlineStatus;
                if (!_onlineStatus.Online)
                {
                    var offLineDuration = DateTime.UtcNow - _onlineStatus.LastLogout;
                    if (offLineDuration.TotalDays > 365)
                    {
                        _offLineTime = $"{offLineDuration.TotalDays / 365:N0}y{offLineDuration.TotalDays % 365 / 30 :N1}mo";
                    }
                    else
                    {
                        if(offLineDuration.TotalDays > 30)
                        {
                            _offLineTime = $"{offLineDuration.TotalDays / 30:N0}mo{offLineDuration.TotalDays % 30:N1}d";
                        }
                        else
                        {
                            if(offLineDuration.TotalDays > 1)
                            {
                                _offLineTime = $"{offLineDuration.Days}d{offLineDuration.Hours + offLineDuration.Minutes / 60.0 :N1}h";
                            }
                            else
                            {
                                _offLineTime = $"{offLineDuration.Hours}h{offLineDuration.Minutes:N0}min";
                            }
                        }
                    }
                }
                _corporation = corporation;
                _alliance = alliance;
                #region skill queue
                //技能队列要么都在训练，要么都暂停，没有某些训练某些暂停的情况
                bool running = false;
                int unFinishCount = 0;
                DateTime firstStartDateTime = DateTime.MaxValue;
                DateTime lastFinishDateTime = DateTime.MinValue;
                foreach (var skill in skillQueueItems)
                {
                    var finishDateTime = string.IsNullOrEmpty(skill.FinishDate) ? DateTime.MinValue : DateTime.Parse(skill.FinishDate);//已经是本地时间
                    if(finishDateTime > lastFinishDateTime)
                    {
                        lastFinishDateTime = finishDateTime;
                    }
                    var startDateTime = string.IsNullOrEmpty(skill.StartDate) ? DateTime.MinValue : DateTime.Parse(skill.StartDate);//已经是本地时间
                    if (startDateTime < firstStartDateTime)
                    {
                        firstStartDateTime = startDateTime;
                    }
                    var isFinished = finishDateTime != DateTime.MinValue && finishDateTime < DateTime.Now;
                    var isWaiting = startDateTime != DateTime.MinValue && finishDateTime != DateTime.MinValue && startDateTime > DateTime.Now;
                    var isPause = string.IsNullOrEmpty(skill.FinishDate) || string.IsNullOrEmpty(skill.StartDate);
                    var isRunning = !(isFinished || isWaiting || isPause);
                    running = running || isRunning;
                    if (!isFinished)
                    {
                        unFinishCount++;
                    }
                }
                _skillQueueRunning = running;
                if (running && lastFinishDateTime > DateTime.MinValue)
                {
                    var remainTime = lastFinishDateTime - DateTime.Now;
                    _skillQueueRemainTime = $"{remainTime.Days}d{remainTime.Hours}h{remainTime.Minutes}min";
                    if (firstStartDateTime < DateTime.MaxValue)
                    {
                        var totalTime = lastFinishDateTime - firstStartDateTime;
                        _skillQueueRemainRatio = (remainTime / totalTime * 100).ToString("N0");
                    }
                }
                _skillQueueTotalCount = skillQueueItems.Count;
                _skillQueueUndoneCount = unFinishCount;
                
                #endregion
            });
        }

        public async Task GetZKBInfoAsync()
        {
            try
            {
                var statistic = await ZKB.NET.ZKB.GetStatisticAsync(ZKB.NET.EntityType.CharacterID, SelectedCharacter.CharacterID);
                if (statistic != null)
                {
                    HasZKB = true;
                    ItemLost = statistic.ItemLost;
                    ItemDestroyed = statistic.ItemDestroyed;
                    ISKLost = statistic.ISKLost;
                    ISKDestroyed = statistic.ISKDestroyed;
                    SoloKills = statistic.SoloKills;
                    DangerRatio = statistic.DangerRatio;
                    GangRatio = statistic.GangRatio;
                }
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                Window?.ShowError(ex.Message);
            }
        }
    }
}
