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
using TheGuideToTheNewEden.Core.Extensions;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.WinUI.Extensions;
using CommunityToolkit.WinUI.UI.Controls;


namespace TheGuideToTheNewEden.WinUI.Views.Home
{
    public class WalletStatisticModel
    {
        public string Name { get => Helpers.ResourcesHelper.GetString(_resourceName); }
        public double Percent { get; set; }
        public Brush Brush { get; set; }
        public double Wallet { get; set; }

        private string _resourceName;
        public WalletStatisticModel(string name, Brush brush)
        {
            _resourceName = name;
            Brush = brush;
        }
    }
    public sealed partial class WalletStatistic : UserControl
    {
        private readonly Brush _greenBrush;
        private readonly Brush _redBrush;
        public WalletStatistic()
        {
            _greenBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 97, 197, 171));
            _redBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 229, 101, 91));
            this.InitializeComponent();
            Loaded += WalletStatistic_Loaded;
        }

        private void WalletStatistic_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= WalletStatistic_Loaded;
            Init();
        }
        private void Init()
        {
            LoadingUI.IsActive = true;
            LoadingUI.Visibility = Visibility.Visible;
            LoadDataButton.Visibility = Visibility.Collapsed;
            TipTextBlock.Visibility = Visibility.Collapsed;
            ContentGrid.Visibility = Visibility.Collapsed;

            var characterDatas = Services.CharacterService.CharacterOauths;
            if (characterDatas.NotNullOrEmpty())
            {
                object locker = new object();
                int error = 0;
                double balance = 0;
                WalletStatisticModel marketStatistic_in = new WalletStatisticModel("HomePage_Wallet_Type_Market", _greenBrush);
                WalletStatisticModel missionStatistic_in = new WalletStatisticModel("HomePage_Wallet_Type_Mission", _greenBrush);
                WalletStatisticModel bountyStatistic_in = new WalletStatisticModel("HomePage_Wallet_Type_Bounty", _greenBrush);
                WalletStatisticModel contractStatistic_in = new WalletStatisticModel("HomePage_Wallet_Type_Contract", _greenBrush);
                WalletStatisticModel donationStatistic_in = new WalletStatisticModel("HomePage_Wallet_Type_Donation", _greenBrush);
                WalletStatisticModel otherStatistic_in = new WalletStatisticModel("HomePage_Wallet_Type_Other", _greenBrush);

                WalletStatisticModel marketStatistic_out = new WalletStatisticModel("HomePage_Wallet_Type_Market", _redBrush);
                WalletStatisticModel contractStatistic_out = new WalletStatisticModel("HomePage_Wallet_Type_Contract", _redBrush);
                WalletStatisticModel taxStatistic_out = new WalletStatisticModel("HomePage_Wallet_Type_Tax", _redBrush);
                WalletStatisticModel donationStatistic_out = new WalletStatisticModel("HomePage_Wallet_Type_Donation", _redBrush);
                WalletStatisticModel otherStatistic_out = new WalletStatisticModel("HomePage_Wallet_Type_Other", _redBrush);
                List<WalletStatisticModel> walletStatistic_in = new List<WalletStatisticModel>();
                List<WalletStatisticModel> walletStatistic_out = new List<WalletStatisticModel>();
                double inWallet = 0;
                double outWallet = 0;
                double diffWallet = 0;
                List<Task> tasks = new List<Task>();
                foreach (var characterData in characterDatas)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            if (!characterData.IsTokenValid())
                            {
                                if (!characterData.RefreshTokenAsync().Result)
                                {
                                    lock (locker)
                                    {
                                        error++;
                                        return;
                                    }
                                }
                            }
                            EVEStandard.EVEStandardAPI esiClient = ESIService.GetDefaultESI2();
                            var auth = ESIService.ToEVEStandardSSO(characterData);
                            var walletResult = esiClient.Wallet.GetCharacterWalletBalanceV1Async(auth).Result;
                            if (walletResult.Model > 0)
                            {
                                lock (locker)
                                {
                                    balance += walletResult.Model;
                                }
                            }
                            int page = 1;
                            List<EVEStandard.Models.CharacterWalletJournal> journals = new List<EVEStandard.Models.CharacterWalletJournal>();
                            while (true)
                            {
                                var walletJounyResult = esiClient.Wallet.GetCharacterWalletJournalV6Async(auth, page).Result;
                                if (walletJounyResult.Model.NotNullOrEmpty())
                                {
                                    journals.AddRange(walletJounyResult.Model);
                                }
                                if(page >= walletJounyResult.MaxPages)
                                {
                                    break;
                                }
                                else
                                {
                                    page++;
                                }
                            }

                            lock (locker)
                            {
                                string journalType = null;
                                foreach (var journal in journals)
                                {
                                    journalType = journal.RefType.ToString();
                                    if (journal.Amount == null)
                                    {
                                        continue;
                                    }
                                    double amount = journal.Amount.Value;
                                    if(journal.RefType == EVEStandard.Enumerations.TransactionType.player_donation)
                                    {
                                        if (amount > 0)
                                            donationStatistic_in.Wallet += amount;
                                        else
                                            donationStatistic_out.Wallet += amount;
                                    }
                                    else if (journalType.Contains("tax") || journalType.Contains("fee") || journal.RefType == EVEStandard.Enumerations.TransactionType.player_trading)
                                    {
                                        if (amount > 0)
                                            otherStatistic_in.Wallet += amount;
                                        else
                                            taxStatistic_out.Wallet += amount;
                                    }
                                    else if (journalType.Contains("market") || journalType.Contains("store"))
                                    {
                                        if (amount > 0)
                                            marketStatistic_in.Wallet += amount;
                                        else
                                            marketStatistic_out.Wallet += amount;
                                    }
                                    else if (journalType.Contains("agent") || journalType.Contains("mission"))
                                    {
                                        if (amount > 0)
                                            missionStatistic_in.Wallet += amount;
                                        else
                                            otherStatistic_out.Wallet += amount;
                                    }
                                    else if (journalType.Contains("bounty"))
                                    {
                                        if (amount > 0)
                                            bountyStatistic_in.Wallet += amount;
                                        else
                                            otherStatistic_out.Wallet += amount;
                                    }
                                    else if (journalType.Contains("contract"))
                                    {
                                        if (amount > 0)
                                            contractStatistic_in.Wallet += amount;
                                        else
                                            contractStatistic_out.Wallet += amount;
                                    }
                                    else
                                    {
                                        if (amount > 0)
                                            otherStatistic_in.Wallet += amount;
                                        else
                                            otherStatistic_out.Wallet += amount;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Core.Log.Error(ex);
                            lock (locker)
                            {
                                error++;
                            }
                        }
                    }));
                }
                Task.WhenAll(tasks).ContinueWith((task) =>
                {
                    lock (locker)
                    {
                        if(marketStatistic_in.Wallet != 0)
                            walletStatistic_in.Add(marketStatistic_in);
                        if (missionStatistic_in.Wallet != 0)
                            walletStatistic_in.Add(missionStatistic_in);
                        if (bountyStatistic_in.Wallet != 0)
                            walletStatistic_in.Add(bountyStatistic_in);
                        if (contractStatistic_in.Wallet != 0)
                            walletStatistic_in.Add(contractStatistic_in);
                        if (donationStatistic_in.Wallet != 0)
                            walletStatistic_in.Add(donationStatistic_in);
                        if (otherStatistic_in.Wallet != 0)
                            walletStatistic_in.Add(otherStatistic_in);
                        inWallet = walletStatistic_in.Sum(x => x.Wallet);
                        if (inWallet != 0)
                        {
                            foreach (var p in walletStatistic_in)
                            {
                                p.Percent = p.Wallet / inWallet * 100;
                            }
                        }

                        if (marketStatistic_out.Wallet != 0)
                            walletStatistic_out.Add(marketStatistic_out);
                        if (contractStatistic_out.Wallet != 0)
                            walletStatistic_out.Add(contractStatistic_out);
                        if (taxStatistic_out.Wallet != 0)
                            walletStatistic_out.Add(taxStatistic_out);
                        if (donationStatistic_out.Wallet != 0)
                            walletStatistic_out.Add(donationStatistic_out);
                        if (otherStatistic_out.Wallet != 0)
                            walletStatistic_out.Add(otherStatistic_out);
                        outWallet = walletStatistic_out.Sum(x => x.Wallet);
                        if (outWallet != 0)
                        {
                            foreach (var p in walletStatistic_out)
                            {
                                if (p.Wallet != 0)
                                {
                                    p.Percent = p.Wallet / outWallet * 100;
                                }
                            }
                        }
                        diffWallet = inWallet + outWallet;
                    }
                    Helpers.WindowHelper.MainWindow.DispatcherQueue.SafelyTryEnqueue(() =>
                    {
                        LoadingUI.IsActive = false;
                        LoadingUI.Visibility = Visibility.Collapsed;
                        LoadDataButton.Visibility = Visibility.Visible;
                        ContentGrid.Visibility = Visibility.Visible;
                        WalletSumTextBlock.Text = balance.ToString("N2");

                        WalletListView_In.ItemsSource = walletStatistic_in;
                        WalletListView_Out.ItemsSource = walletStatistic_out;
                        InWalletTextBlock.Text = inWallet.ToString("N2");
                        OutWalletTextBlock.Text = outWallet.ToString("N2");
                        string diffWalletStr = diffWallet.ToString("N2");
                        if (diffWallet > 0)
                        {
                            diffWalletStr = "+" + diffWallet.ToString("N2");
                        }
                        DiffWalletTextBlock.Text = diffWalletStr;
                        DiffWalletTextBlock.Foreground = diffWallet < 0 ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 229, 101, 91)) : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 97, 197, 171));
                    });
                });
            }
            else
            {
                LoadingUI.IsActive = false;
                LoadingUI.Visibility = Visibility.Collapsed;
                LoadDataButton.Visibility = Visibility.Visible;
                TipTextBlock.Visibility = Visibility.Visible;
                TipTextBlock.Text = Helpers.ResourcesHelper.GetString("HomePage_NoCharacterTip");
            }
        }

        private void LoadDataButton_Click(object sender, RoutedEventArgs e)
        {
            Init();
        }
    }
}
