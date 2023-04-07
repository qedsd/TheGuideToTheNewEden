using CommunityToolkit.Mvvm.Input;
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
using TheGuideToTheNewEden.WinUI.Helpers;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class CharacterViewModel: BaseViewModel
    {
        #region 属性
        private ObservableCollection<CharacterOauth> characters;
        public ObservableCollection<CharacterOauth> Characters
        {
            get => characters;
            set => SetProperty(ref characters, value); 
        }
        private CharacterOauth selectedCharacter;
        public CharacterOauth SelectedCharacter
        {
            get => selectedCharacter;
            set
            {
                if (selectedCharacter != null)
                {
                    selectedCharacter.IsActive = false;
                }
                SetProperty(ref selectedCharacter, value);
                if (selectedCharacter != null)
                {
                    value.IsActive = true;
                    GetBaseInfoAsync(value);
                }
            }
        }
        private long sp;
        public long SP { get => sp; set => SetProperty(ref sp, value); }

        private double isk;
        public double ISK { get => isk; set => SetProperty(ref isk, value); }

        private int lp;
        public int LP { get => lp; set => SetProperty(ref lp, value); }

        private Skill skill;
        public Skill Skill { get => skill; set => SetProperty(ref skill, value); }

        private List<Loyalty> loyalties;
        public List<Loyalty> Loyalties { get => loyalties; set => SetProperty(ref loyalties, value); }

        private OnlineStatus onlineStatus;
        public OnlineStatus OnlineStatus { get => onlineStatus; set => SetProperty(ref onlineStatus, value); }

        private Location location;
        public Location Location { get => location; set => SetProperty(ref location, value); }

        private StayShip stayShip;
        public StayShip StayShip { get => stayShip; set => SetProperty(ref stayShip, value); }

        private Affiliation affiliation;
        public Affiliation Affiliation { get => affiliation; set => SetProperty(ref affiliation, value); }

        private List<SkillQueue> skillQueues;
        public List<SkillQueue> SkillQueues { get => skillQueues; set => SetProperty(ref skillQueues, value); }

        private Skill queue;
        public Skill Queue { get => queue; set => SetProperty(ref queue, value); }

        private SkillQueue firstSkillQueue;
        public SkillQueue FirstSkillQueue { get => firstSkillQueue; set => SetProperty(ref firstSkillQueue, value); }

        private int doneSkillCount;
        public int DoneSkillCount { get => doneSkillCount; set => SetProperty(ref doneSkillCount, value); }

        private int traingSkillCount;
        public int TraingSkillCount { get => traingSkillCount;set => SetProperty(ref  traingSkillCount, value); }

        private int mainPivotIndex;
        public int MainPivotIndex
        {
            get => mainPivotIndex;
            set
            {
                SetProperty(ref  mainPivotIndex, value);
            }
        }
        #endregion
        public ICommand AddCommand => new RelayCommand(async() =>
        {
            AuthHelper.RegistyProtocol();
            Window.ShowWaiting("等待网页授权...");
            var result = await AuthHelper.WaitingAuthAsync();
            Window.HideWaiting();
        });
        private void ResetCharacter()
        {
            
        }
        private async void GetBaseInfoAsync(CharacterOauth characterOauth)
        {
            if (characterOauth == null)
            {
                return;
            }
            Window.ShowWaiting();
            ResetCharacter();
            string token = await characterOauth.GetAccessTokenAsync();
            Skill skill = null;
            double isk = 0;
            List<Loyalty> loyalties = null;
            OnlineStatus onlineStatus = null;
            Location location = null;
            StayShip ship = null;
            Affiliation affiliation = null;
            List<SkillQueue> skillQueues = null;
            var tasks = new Task[]
            {
                Core.Services.CharacterService.GetSkillWithGroupAsync(characterOauth.CharacterID, token).ContinueWith((p)=>
                {
                    skill = p.Result;
                }),
                Core.Services.CharacterService.GetWalletBalanceAsync(characterOauth.CharacterID, token).ContinueWith((p)=>
                {
                    isk = p.Result;
                }),
                Core.Services.CharacterService.GetLoyaltysAsync(characterOauth.CharacterID, token).ContinueWith((p)=>
                {
                    loyalties = p.Result;
                }),
                Core.Services.CharacterService.GetOnlineStatusAsync(characterOauth.CharacterID, token).ContinueWith((p)=>
                {
                    onlineStatus = p.Result;
                }),
                Core.Services.CharacterService.GetLocationAsync(characterOauth.CharacterID, token).ContinueWith((p)=>
                {
                    location = p.Result;
                }),
                Core.Services.CharacterService.GetStayShipAsync(characterOauth.CharacterID, token).ContinueWith((p)=>
                {
                    ship = p.Result;
                }),
                Core.Services.OrganizationService.GetAffiliationAsync(characterOauth.CharacterID).ContinueWith((p)=>
                {
                    affiliation = p.Result;
                }),
                Core.Services.CharacterService.GetSkillQueuesAsync(characterOauth.CharacterID, token).ContinueWith((p)=>
                {
                    skillQueues = p.Result;
                }),
            };
            await Task.WhenAll(tasks);
            
            DoneSkillCount = 0;
            TraingSkillCount = 0;
            if (skillQueues != null)
            {
                DoneSkillCount = skillQueues.Where(p => p.IsDone).Count();
                TraingSkillCount = skillQueues.Count - DoneSkillCount;
            }
            Window.DispatcherQueue.TryEnqueue(() =>
            {
                ISK = isk;
                LP = loyalties == null ? 0 : loyalties.Sum(p => p.Loyalty_points);
                Skill = skill;
                Loyalties = loyalties;
                OnlineStatus = onlineStatus;
                Location = location;
                StayShip = ship;
                Affiliation = affiliation;
                SkillQueues = skillQueues;
                FirstSkillQueue = SkillQueues?.FirstOrDefault(p => p.IsTraing);
                if (FirstSkillQueue == null)
                {
                    FirstSkillQueue = SkillQueues?.FirstOrDefault();
                }
            });
            Window.HideWaiting();
        }
    }
}
