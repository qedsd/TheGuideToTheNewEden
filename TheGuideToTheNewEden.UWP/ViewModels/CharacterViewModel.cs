using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.Models.Character;

namespace TheGuideToTheNewEden.UWP.ViewModels
{
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public class CharacterViewModel : ObservableObject
    {
        public ObservableCollection<CharacterOauth> CharacterOauths = Services.CharacterService.CharacterOauths;
        private CharacterOauth characterOauth;
        public CharacterOauth CharacterOauth
        {
            get => characterOauth;
            set
            {
                SetProperty(ref characterOauth, value);
                GetBaseInfo(value);
            }
        }
        public Core.Enums.GameServerType GameServerType { get; set; } =  Services.GameServerSelectorService.GameServerType;
        public long SP { get; set; }
        public double ISK { get; set; }
        public int LP { get; set; }

        public Skill Skill { get; set; }
        public List<Loyalty> Loyalties { get; set; }
        public OnlineStatus OnlineStatus { get; set; }
        public Location Location { get; set; }
        public StayShip StayShip { get; set; }
        public Affiliation Affiliation { get; set; }
        public List<SkillQueue> SkillQueues { get; set; }
        public SkillQueue FirstSkillQueue { get => SkillQueues.FirstOrDefault(); }
        private int mainPivotIndex;
        public int MainPivotIndex
        {
            get => mainPivotIndex;
            set
            {
                mainPivotIndex = value;

            }
        }
        public CharacterViewModel()
        {
            CharacterOauth = CharacterOauths.FirstOrDefault();
            CharacterOauths.CollectionChanged += CharacterOauths_CollectionChanged;
        }

        private void CharacterOauths_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if(e.NewItems?.Count > 0)
            {
                CharacterOauth = e.NewItems[0] as CharacterOauth;
            }
            else if(e.OldItems?.Count > 0)
            {
                if(e.OldItems[0] == CharacterOauth)
                {
                    CharacterOauth = CharacterOauths.FirstOrDefault();
                }
            }
        }

        public ICommand AddCommand => new RelayCommand(() =>
        {
            Services.CharacterService.GetAuthorizeByBrower();
        });

        public ICommand RemoveCommand => new RelayCommand<Windows.UI.Xaml.Controls.MenuFlyoutItem>(async(item) =>
        {
            if(item != null)
            {
                Controls.WaitingPopup.Show();
                await Services.CharacterService.RemoveAsync(item.DataContext as CharacterOauth);
                Controls.WaitingPopup.Hide();
            }
        });

        private async void GetBaseInfo(CharacterOauth characterOauth)
        {
            if(characterOauth == null)
            {
                return;
            }
            Controls.WaitingPopup.Show();
            var skill = await Core.Services.CharacterService.GetSkillWithGroupAsync(characterOauth.CharacterID, await characterOauth.GetAccessToken());
            var isk = await Core.Services.CharacterService.GetWalletBalanceAsync(characterOauth.CharacterID, await characterOauth.GetAccessToken());
            var loyalties = await Core.Services.CharacterService.GetLoyaltysAsync(characterOauth.CharacterID, await characterOauth.GetAccessToken());
            var onlineStatus = await Core.Services.CharacterService.GetOnlineStatusAsync(characterOauth.CharacterID, await characterOauth.GetAccessToken());
            var location = await Core.Services.CharacterService.GetLocationAsync(characterOauth.CharacterID, await characterOauth.GetAccessToken());
            var ship = await Core.Services.CharacterService.GetStayShipAsync(characterOauth.CharacterID, await characterOauth.GetAccessToken());
            var affiliation = await Core.Services.OrganizationService.GetAffiliationAsync(characterOauth.CharacterID);
            var skillQueues = await Core.Services.CharacterService.GetSkillQueuesAsync(characterOauth.CharacterID, await characterOauth.GetAccessToken());
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High,
            () =>
            {
                //SP = skill.Total_sp;
                ISK = isk;
                LP = loyalties == null? 0: loyalties.Sum(p => p.Loyalty_points);
                Skill = skill;
                Loyalties = loyalties;
                OnlineStatus = onlineStatus;
                Location = location;
                StayShip = ship;
                Affiliation = affiliation;
                SkillQueues = skillQueues;
            });
            Controls.WaitingPopup.Hide();
        }
    }
}
