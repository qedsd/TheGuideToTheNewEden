using CommunityToolkit.Mvvm.Input;
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
using EVEStandard;
using Newtonsoft.Json.Linq;
using EVEStandard.Enumerations;
using Microsoft.Extensions.Options;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Dialogs;
using TheGuideToTheNewEden.WinUI.Converters;
using Vanara.PInvoke;
using TheGuideToTheNewEden.WinUI.Extensions;
using Newtonsoft.Json;
using EVEStandard.Models.API;
using EVEStandard.Models;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class CharacterViewModel: BaseViewModel, IDisposable
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
        private double _characterWallet;
        private long _lp;
        private EVEStandard.Models.CharacterInfo _information;
        private EVEStandard.Models.CharacterSkills _skill;
        private List<EVEStandard.Models.LoyaltyPoints> _loyaltyPoints;
        private EVEStandard.Models.CharacterOnline _onlineStatus;
        private EVEStandard.Models.CharacterLocation _location;
        private EVEStandard.Models.CharacterShip _ship;
        private List<EVEStandard.Models.CorporationWallet> _corpWallets;
        private double _corpWallet;
        private string _offLineTime;
        private bool _skillQueueRunning;
        private string _skillQueueRemainRatio = "0";
        private string _skillQueueRemainTime;
        private int _skillQueueTotalCount;
        private int _skillQueueUndoneCount;
        private EVEStandard.Models.CorporationInfo _corporation = null;
        private EVEStandard.Models.Alliance _alliance = null;
        #endregion

        #region 详细页属性
        private BitmapImage _characterAvatar;
        public BitmapImage CharacterAvatar { get => _characterAvatar; set => SetProperty(ref _characterAvatar, value); }
        public double CharacterWallet { get => _characterWallet; set => SetProperty(ref _characterWallet, value); }
        public long LP { get => _lp; set => SetProperty(ref _lp, value); }
        public EVEStandard.Models.CharacterInfo Information { get => _information; set => SetProperty(ref _information, value); }
        public EVEStandard.Models.CharacterSkills Skill { get => _skill; set => SetProperty(ref _skill, value); }
        public List<EVEStandard.Models.LoyaltyPoints> LoyaltyPoints { get => _loyaltyPoints; set => SetProperty(ref _loyaltyPoints, value); }
        public EVEStandard.Models.CharacterOnline OnlineStatus { get => _onlineStatus; set => SetProperty(ref _onlineStatus, value); }
        public EVEStandard.Models.CharacterLocation Location { get => _location; set => SetProperty(ref _location, value); }
        public EVEStandard.Models.CharacterShip Ship { get => _ship; set => SetProperty(ref _ship, value); }
        public List<EVEStandard.Models.CorporationWallet> CorpWallets { get => _corpWallets; set => SetProperty(ref _corpWallets, value); }
        public double CorpWallet { get => _corpWallet; set => SetProperty(ref _corpWallet, value); }
        public EVEStandard.Models.CorporationInfo Corporation { get => _corporation; set => SetProperty(ref _corporation, value); }
        public EVEStandard.Models.Alliance Alliance { get => _alliance; set => SetProperty(ref _alliance, value); }
        #endregion

        #region 卡片页属性
        private BitmapImage _characterAvatar_Card;
        public BitmapImage CharacterAvatar_Card { get => _characterAvatar_Card; set => SetProperty(ref _characterAvatar_Card, value); }
        public string CharacterWallet_Card { get => _characterWallet.ToString("N2"); }
        public long LP_Card { get => _lp; set => SetProperty(ref _lp, value); }
        public EVEStandard.Models.CharacterInfo Information_Card { get => _information; set => SetProperty(ref _information, value); }
        public string TotalSP { get => _skill?.TotalSp.ToString("N0"); }
        public EVEStandard.Models.CharacterSkills Skill_Card { get => _skill; set => SetProperty(ref _skill, value); }
        public List<EVEStandard.Models.LoyaltyPoints> LoyaltyPoints_Card { get => _loyaltyPoints; set => SetProperty(ref _loyaltyPoints, value); }
        public EVEStandard.Models.CharacterOnline OnlineStatus_Card { get => _onlineStatus; set => SetProperty(ref _onlineStatus, value); }
        public EVEStandard.Models.CharacterLocation Location_Card { get => _location; set => SetProperty(ref _location, value); }
        public EVEStandard.Models.CharacterShip Ship_Card { get => _ship; set => SetProperty(ref _ship, value); }
        public List<EVEStandard.Models.CorporationWallet> CorpWallets_Card { get => _corpWallets; set => SetProperty(ref _corpWallets, value); }
        public double CorpWallet_Card { get => _corpWallet; set => SetProperty(ref _corpWallet, value); }
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

        private EVEStandardAPI _api;
        public EVEStandardAPI EsiClient => _api;
        public CharacterViewModel()
        {
            _api = ESIService.GetDefaultESI();
        }
        public CharacterViewModel(AuthorizedCharacterData characterData)
        {
            SelectedCharacter = characterData;
            _api = ESIService.GetDefaultESI();
        }
        public void Init()
        {
            GetBaseInfoAsync().Wait();
        }
        public ICommand RefreshCommand => new RelayCommand(async() =>
        {
            ShowWaiting();
            await GetBaseInfoAsync();
            await GetZKBInfoAsync();
            HideWaiting();
        });
        public ICommand ZKBCommand => new RelayCommand(async () =>
        {
            await ClientServiceHelper.GetRequiredService<KBNavigationService>().NavigationTo((int)SelectedCharacter.CharacterID, ZKB.NET.EntityType.CharacterID, SelectedCharacter.CharacterName);
        });
        private async Task GetBaseInfoAsync()
        {
            var characterData = _selectedCharacter;
            if (!characterData.IsTokenValid())
            {
                if (!await characterData.RefreshTokenAsync())
                {
                    HideWaiting();
                    Helpers.WindowHelper.MainWindow.DispatcherQueue.SafelyTryEnqueue(() =>
                    {
                        ShowError($"{SelectedCharacter.CharacterName}: {Helpers.ResourcesHelper.GetString("CharacterPage_TryUpdateTokenFailed")}({Core.Log.GetLastError()})");
                    });
                    return;
                }
            }
            EVEStandard.Models.CharacterInfo information = null;
            EVEStandard.Models.CharacterSkills skill = null;
            List<EVEStandard.Models.LoyaltyPoints> loyalties = null;
            
            double characterWallet = 0;
            List<EVEStandard.Models.CorporationWallet> corpWallets = null;
            List<EVEStandard.Models.SkillQueue> skillQueueItems = null;
            EVEStandard.Models.CharacterOnline onlineStatus = null;

            EVEStandard.Models.CorporationInfo corporation = null;
            EVEStandard.Models.Alliance alliance = null;
            // 并发启动所有请求
            var infoTask = _api.Character.GetCharacterPublicInfoAsync(characterData.CharacterID);
            var skillsTask = _api.Skills.GetCharacterSkillsAsync(characterData.Auth);
            var loyaltyTask = _api.Loyalty.GetLoyaltyPointsAsync(characterData.Auth);
            var walletTask = _api.Wallet.GetCharacterWalletBalanceAsync(characterData.Auth);
            //var corpWalletTask = _api.Wallet.ReturnCorporationWalletBalanceAsync(characterData.Auth, characterData.CorporationID);
            var queueTask = _api.Skills.GetCharacterSkillQueueAsync(characterData.Auth);
            var onlineTask = _api.Location.GetCharacterOnlineAsync(characterData.Auth);
            //var corpTask = _api.Corporation.GetCorporationInfoAsync(characterData.CorporationID);
            //Task<EVEStandard.Models.API.ESIModelDTO<EVEStandard.Models.Alliance>> allianceTask = null;
            //if (characterData.AllianceID > 0)
            //{
            //    allianceTask = _api.Alliance.GetAllianceInfoAsync(characterData.AllianceID);
            //}
            try
            {
                var allTasks = new List<Task> { infoTask, skillsTask, loyaltyTask, walletTask,queueTask, onlineTask};

                await Task.WhenAll(allTasks);

                information = (await infoTask).Model;
                skill = (await skillsTask).Model;
                loyalties = (await loyaltyTask).Model;
                characterWallet = (await walletTask).Model;
                skillQueueItems = (await queueTask).Model;
                onlineStatus = (await onlineTask).Model;

                if(information != null)
                {
                    allTasks.Clear();
                    Task<ESIModelDTO<List<CorporationWallet>>> corpWalletTask = null;
                    Task<ESIModelDTO<CorporationInfo>> corpTask = null;
                    Task<ESIModelDTO<Alliance>> allianceTask = null;
                    if (information.CorporationId > 0)
                    {
                        characterData.CorporationID = information.CorporationId;
                        corpWalletTask = _api.Wallet.ReturnCorporationWalletBalanceAsync(characterData.Auth, characterData.CorporationID);
                        corpTask = _api.Corporation.GetCorporationInfoAsync(characterData.CorporationID);
                        allTasks.Add(corpWalletTask);
                        allTasks.Add(corpTask);
                    }
                    if(information.AllianceId > 0)
                    {
                        characterData.AllianceID = information.AllianceId.Value;
                        allianceTask = _api.Alliance.GetAllianceInfoAsync(characterData.AllianceID);
                    }
                    await Task.WhenAll(allTasks);
                    if(corpWalletTask != null)
                    {
                        corpWallets = (await corpWalletTask).Model;
                        corporation = (await corpTask).Model;
                    }
                    alliance = allianceTask == null ? null : (await allianceTask).Model;
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                ShowError(ex.Message);
            }
            Window.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                _characterAvatar = new BitmapImage(new System.Uri(GameImageConverter.GetImageUri((int)SelectedCharacter.CharacterID, GameImageConverter.ImgType.Character, 512)));
                _characterAvatar_Card = new BitmapImage(new System.Uri(GameImageConverter.GetImageUri((int)SelectedCharacter.CharacterID, GameImageConverter.ImgType.Character, 128)));
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
                    _lp = loyalties.Sum(p => p.Points);
                }
                else
                {
                    _lp = 0;
                }
                _onlineStatus = onlineStatus;
                if (_onlineStatus != null && !_onlineStatus.Online)
                {
                    var offLineDuration = DateTime.UtcNow - (DateTime)_onlineStatus.LastLogout;
                    if (offLineDuration.TotalDays > 365)
                    {
                        _offLineTime = $"{offLineDuration.TotalDays / 365:N0}y {offLineDuration.TotalDays % 365 / 30 :N1}mo";
                    }
                    else
                    {
                        if(offLineDuration.TotalDays > 30)
                        {
                            _offLineTime = $"{offLineDuration.TotalDays / 30:N0}mo {offLineDuration.TotalDays % 30:N1}d";
                        }
                        else
                        {
                            if(offLineDuration.TotalDays > 1)
                            {
                                _offLineTime = $"{offLineDuration.Days}d {offLineDuration.Hours + offLineDuration.Minutes / 60.0 :N1}h";
                            }
                            else
                            {
                                _offLineTime = $"{offLineDuration.Hours}h {offLineDuration.Minutes:N0}min";
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
                if (skillQueueItems.NotNullOrEmpty())
                {
                    _skillQueueTotalCount = skillQueueItems.Count;
                    foreach (var skill in skillQueueItems)
                    {
                        var finishDateTime = skill.FinishDate == null ? DateTime.MinValue : (DateTime)skill.FinishDate;//已经是本地时间
                        if (finishDateTime > lastFinishDateTime)
                        {
                            lastFinishDateTime = finishDateTime;
                        }
                        var startDateTime = skill.StartDate == null ? DateTime.MinValue : (DateTime)skill.StartDate;//已经是本地时间
                        if (startDateTime < firstStartDateTime)
                        {
                            firstStartDateTime = startDateTime;
                        }
                        var isFinished = finishDateTime != DateTime.MinValue && finishDateTime < DateTime.Now;
                        var isWaiting = startDateTime != DateTime.MinValue && finishDateTime != DateTime.MinValue && startDateTime > DateTime.Now;
                        var isPause = skill.FinishDate != null || skill.StartDate != null || (skill.FinishDate == null && skill.StartDate == null);
                        var isRunning = !(isFinished || isWaiting || isPause);
                        running = running || isRunning;
                        if (!isFinished)
                        {
                            unFinishCount++;
                        }
                    }
                }
                
                _skillQueueRunning = running;
                if (running && lastFinishDateTime > DateTime.MinValue)
                {
                    var remainTime = lastFinishDateTime - DateTime.Now;
                    _skillQueueRemainTime = $"{remainTime.Days}d {remainTime.Hours}h {remainTime.Minutes}min";
                    if (firstStartDateTime < DateTime.MaxValue)
                    {
                        var totalTime = lastFinishDateTime - firstStartDateTime;
                        _skillQueueRemainRatio = (remainTime / totalTime * 100).ToString("N0");
                    }
                }
                
                _skillQueueUndoneCount = unFinishCount;
                
                #endregion
            });
        }

        public async Task GetZKBInfoAsync()
        {
            try
            {
                var statistic = await ZKB.NET.ZKB.GetStatisticAsync(ZKB.NET.EntityType.CharacterID, (int)SelectedCharacter.CharacterID);
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
                ShowError(ex.Message);
            }
        }

        public void Dispose()
        {
            
        }
    }
}
