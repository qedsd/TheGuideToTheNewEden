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
using TheGuideToTheNewEden.UWP.Helpers;

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
                if (characterOauth != null)
                {
                    characterOauth.IsActive = false;
                }
                SetProperty(ref characterOauth, value);
                value.IsActive = true;
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
        public SkillQueue FirstSkillQueue { get;private set; }
        public int DoneSkillCount { get; set; }
        public int TraingSkillCount { get; set; }
        private int mainPivotIndex;
        public int MainPivotIndex
        {
            get => mainPivotIndex;
            set
            {
                mainPivotIndex = value;
                switch(value)
                {
                    case 2:TryInitWallet();break;
                    case 3:TryInitMail();break;
                }
            }
        }
        public CharacterViewModel()
        {
            CharacterOauth = CharacterOauths.FirstOrDefault(p=>p.IsActive);
            if(CharacterOauth == null)
            {
                CharacterOauth = CharacterOauths.FirstOrDefault();
            }
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
            
            //var skill = await Core.Services.CharacterService.GetSkillWithGroupAsync(characterOauth.CharacterID, );
            //var isk = await Core.Services.CharacterService.GetWalletBalanceAsync(characterOauth.CharacterID, await characterOauth.GetAccessTokenAsync());
            //var loyalties = await Core.Services.CharacterService.GetLoyaltysAsync(characterOauth.CharacterID, await characterOauth.GetAccessTokenAsync());
            //var onlineStatus = await Core.Services.CharacterService.GetOnlineStatusAsync(characterOauth.CharacterID, await characterOauth.GetAccessTokenAsync());
            //var location = await Core.Services.CharacterService.GetLocationAsync(characterOauth.CharacterID, await characterOauth.GetAccessTokenAsync());
            //var ship = await Core.Services.CharacterService.GetStayShipAsync(characterOauth.CharacterID, await characterOauth.GetAccessTokenAsync());
            //var affiliation = await Core.Services.OrganizationService.GetAffiliationAsync(characterOauth.CharacterID);
            //var skillQueues = await Core.Services.CharacterService.GetSkillQueuesAsync(characterOauth.CharacterID, await characterOauth.GetAccessTokenAsync());
            DoneSkillCount = 0;
            TraingSkillCount = 0;
            if(skillQueues != null)
            {
                DoneSkillCount = skillQueues.Where(p => p.IsDone).Count();
                TraingSkillCount = skillQueues.Count - DoneSkillCount;
            }
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High,
            () =>
            {
                ISK = isk;
                LP = loyalties == null? 0: loyalties.Sum(p => p.Loyalty_points);
                Skill = skill;
                Loyalties = loyalties;
                OnlineStatus = onlineStatus;
                Location = location;
                StayShip = ship;
                Affiliation = affiliation;
                SkillQueues = skillQueues;
                FirstSkillQueue = SkillQueues?.FirstOrDefault(p=>p.IsTraing);
                if(FirstSkillQueue == null)
                {
                    FirstSkillQueue = SkillQueues?.FirstOrDefault();
                }
            });
            Controls.WaitingPopup.Hide();
        }

        private void ResetCharacter()
        {
            MainPivotIndex = 0;

            WalletJournalPage = 0;
            MaxWalletJournalPage = 0;
            Walletjournals = null;

            WalletTransactionPage = 0;
            MaxWalletTransactionPage = 0;
            WalletTransactions = null;

            WalletTransactionStatistic = null;

            WalletPivotIndex = 0;

            MailLabels = null;
            MailLabels = null;
            Mails = null;
        }

        #region wallet

        private int walletPivotIndex;
        public int WalletPivotIndex
        {
            get => walletPivotIndex;
            set
            {
                walletPivotIndex = value;
                switch(value)
                {
                    case 1: TryInitWalletTransaction(); break;
                    case 2: TryInitWalletTransactionStatistic(); break;
                }
            }
        }

        public Controls.NavigationBar.PageChangedDelegate WalletJournalPageChangedEvent => OnWalletJournalPageChanged;
        private void OnWalletJournalPageChanged(int page)
        {
            if(page != 0)
            {
                GetWalletJournal(page);
            }
        }
        public Controls.NavigationBar.PageChangedDelegate WalletTransactionPageChangedEvent => OnWalletTransactionPageChanged;
        private void OnWalletTransactionPageChanged(int page)
        {
            if (page != 0)
            {
                GetWalletTransactions(page);
            }
        }
        public int WalletJournalPage { get; set; }
        public int MaxWalletJournalPage { get; set; }
        public List<Core.Models.Wallet.Walletjournal> Walletjournals { get; set; }
        /// <summary>
        /// 获取钱包日志
        /// </summary>
        /// <param name="page"></param>
        private async void GetWalletJournal(int page = 1)
        {
            Walletjournals = await Core.Services.CharacterService.GetWalletjournalAsync(CharacterOauth.CharacterID, await CharacterOauth.GetAccessTokenAsync(), page);
            if(Walletjournals == null || Walletjournals.Count < 1000)
            {
                MaxWalletJournalPage = 1;
            }
            else
            {
                MaxWalletJournalPage = 0;
            }
        }

        public int WalletTransactionPage { get; set; }
        public int MaxWalletTransactionPage { get; set; }
        public List<Core.Models.Wallet.WalletTransaction> WalletTransactions { get; set; }
        /// <summary>
        /// 获取市场交易
        /// </summary>
        /// <param name="page"></param>
        private async void GetWalletTransactions(int page = 1)
        {
            Controls.WaitingPopup.Show();
            WalletTransactions = await Core.Services.CharacterService.GetWalletTransactionsAsync(CharacterOauth.CharacterID, await CharacterOauth.GetAccessTokenAsync(), page, true);
            Controls.WaitingPopup.Hide();
            if (WalletTransactions == null || WalletTransactions.Count < 1000)
            {
                MaxWalletTransactionPage = 1;
            }
            else
            {
                MaxWalletTransactionPage = 0;
            }
        }
        public List<Core.Models.Wallet.WalletTransaction> WalletTransactionStatistic { get; set; }

        /// <summary>
        /// 获取全部市场交易统计
        /// </summary>
        private async void GetWalletTransactionsStatistic()
        {
            Controls.WaitingPopup.Show();
            List<Core.Models.Wallet.WalletTransaction> wallets = new List<Core.Models.Wallet.WalletTransaction>();
            for (int i=1; ;i++)
            {
                var items = await Core.Services.CharacterService.GetWalletTransactionsAsync(CharacterOauth.CharacterID, await CharacterOauth.GetAccessTokenAsync(), i, false);
                if(items!=null)
                {
                    wallets.AddRange(items);
                }
                if(items.Count < 1000)
                {
                    break;
                }
            }
            Controls.WaitingPopup.Hide();
            WalletTransactionStatistic = await Task.Run(() => WalletTransactionsStatistic(wallets));
        }

        /// <summary>
        /// 统计市场交易
        /// </summary>
        /// <param name="walletTransactions"></param>
        /// <returns></returns>
        private List<Core.Models.Wallet.WalletTransaction> WalletTransactionsStatistic(List<Core.Models.Wallet.WalletTransaction> walletTransactions)
        {
            Dictionary<int, Core.Models.Wallet.WalletTransaction> dic = new Dictionary<int, Core.Models.Wallet.WalletTransaction>();
            foreach(var p in walletTransactions)
            {
                if(dic.TryGetValue(p.Type_id,out var transaction))
                {
                    transaction.Quantity += p.Quantity;
                    transaction.TotalPrice += p.TotalPrice;
                }
                else
                {
                    var clone = p.DepthClone<Core.Models.Wallet.WalletTransaction>();
                    if(clone != null)
                    {
                        clone.TotalPrice = clone.AutoTotalPrice;
                        dic.Add(p.Type_id, clone);
                    }
                }
            }
            return dic.Select(p=>p.Value).ToList();
        }

        private void TryInitWallet()
        {
            if(WalletPivotIndex == 0 && Walletjournals == null)
            {
                WalletJournalPage = 0;
                WalletJournalPage = 1;
            }
        }

        private void TryInitWalletTransaction()
        {
            if (WalletTransactions == null)
            {
                WalletTransactionPage = 1;
            }
        }

        private void TryInitWalletTransactionStatistic()
        {
            if (WalletTransactionStatistic == null)
            {
                GetWalletTransactionsStatistic();
            }
        }
        #endregion

        #region mail
        public List<Core.Models.Mail.MailLabel> MailLabels { get; set; }
        public List<Core.Models.Mail.MailList> MailLists { get; set; }
        public List<Core.Models.Mail.Mail> Mails { get; set; }
        private Core.Models.Mail.MailLabel mailLabel;
        public Core.Models.Mail.MailLabel MailLabel
        {
            get => mailLabel;
            set
            {
                mailLabel = value;
                if(value != null)
                {
                    _ = GetMails(value.Label_id);
                }
            }
        }
        public Core.Models.Mail.MailList mailList;
        public Core.Models.Mail.MailList MailList
        {
            get => mailList;
            set
            {
                mailList = value;
                if(value != null)
                {
                    _= GetMails(value.Mailing_list_id);
                }
            }
        }
        private Core.Models.Mail.Mail mail;
        public Core.Models.Mail.Mail Mail
        {
            get=> mail;
            set
            {
                mail = value;
                if (value != null)
                {
                    ShowMailDetail(Mail);
                }
            }
        }
        private async void TryInitMail()
        {
            if(MailLabels == null)
            {
                Controls.WaitingPopup.Show();
                await GetMailLabels();
                await GetMailLists();
                Controls.WaitingPopup.Hide();
                await GetMails();
            }
        }
        /// <summary>
        /// 获取邮件标签
        /// </summary>
        /// <returns></returns>
        private async Task GetMailLabels()
        {
            var root = await Core.Services.CharacterService.GetMailLabelsAsync(CharacterOauth.CharacterID, await CharacterOauth.GetAccessTokenAsync());
            if(root != null)
            {
                MailLabels = root.Labels;
            }
        }
        /// <summary>
        /// 邮件订阅列表
        /// </summary>
        /// <returns></returns>
        private async Task GetMailLists()
        {
            MailLists = await Core.Services.CharacterService.GetMailListsAsync(CharacterOauth.CharacterID, await CharacterOauth.GetAccessTokenAsync());
        }
        private async Task GetMails(int labelId = -1,int lastMailId = 0)
        {
            Controls.WaitingPopup.Show();
            List<int> labelIds = null;
            if (labelId != -1)
            {
                labelIds = new List<int>();
                labelIds.Add(labelId);
            }
            var mails = await Core.Services.CharacterService.GetMailsAsync(CharacterOauth.CharacterID, await CharacterOauth.GetAccessTokenAsync(), labelIds,lastMailId);
            if(mails!= null)
            {
                var names = await Core.Services.UniverseService.SearchNameByIdsAsync(mails.Select(p => p.From).ToList());
                if (names != null)
                {
                    var dic = names.ToDictionary(p => p.Id);
                    mails.ForEach(p =>
                    {
                        if (dic.TryGetValue(p.From, out var name))
                        {
                            p.From_name = name.Name;
                        }
                    });
                }
            }
            Mails = mails;
            Controls.WaitingPopup.Hide();
        }
        private async void ShowMailDetail(Core.Models.Mail.Mail inputMail)
        {
            Controls.WaitingPopup.Show();
            var mail = await Core.Services.CharacterService.GetMailDetailAsync(CharacterOauth.CharacterID, await CharacterOauth.GetAccessTokenAsync(), inputMail.Mail_id);
            Controls.WaitingPopup.Hide();
            if (mail != null )
            {
                mail.From_name = inputMail.From_name;
                if (mail.Recipients!=null && mail.Recipients.Count > 0)
                {
                    var names = await Core.Services.UniverseService.SearchNameByIdsAsync(mail.Recipients.Select(p => p.Recipient_id).ToList());
                    if (names != null)
                    {
                        var dic = names.ToDictionary(p => p.Id);
                        mail.Recipients.ForEach(p =>
                        {
                            if (dic.TryGetValue(p.Recipient_id, out var name))
                            {
                                p.Recipient_name = name.Name;
                            }
                        });
                    }
                }
                Dialogs.MailDetailDialog mailDetailDialog = new Dialogs.MailDetailDialog(mail);
                await mailDetailDialog.ShowAsync();
            }
            else
            {
                Controls.NotifyPopup.ShowError("Null");
            }
        }
        #endregion
    }
}
