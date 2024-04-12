using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Models;
using TheGuideToTheNewEden.WinUI.Views;
using Windows.ApplicationModel;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class ShellViewModel : BaseViewModel
    {
        private string players;
        public string Players
        {
            get => players;
            set
            {
                SetProperty(ref players, value);
            }
        }
        public string VersionDescription{get;set;}
        public List<ToolItem> ToolItems { get; set; }
        public ShellViewModel()
        {
            VersionDescription = GetVersionDescription();
            ToolItems = new List<ToolItem>()
            {
                new ToolItem(ResourcesHelper.GetString("ShellPage_Character"),ResourcesHelper.GetString("ShellPage_Character_Desc"), typeof(CharacterPage)),
                new ToolItem(ResourcesHelper.GetString("ShellPage_EarlyWarning"),ResourcesHelper.GetString("ShellPage_EarlyWarning_Desc"), typeof(EarlyWarningPage)),
                new ToolItem(ResourcesHelper.GetString("ShellPage_GamePreview"),ResourcesHelper.GetString("ShellPage_GamePreview_Desc"), typeof(GamePreviewMgrPage)),
                new ToolItem(ResourcesHelper.GetString("ShellPage_Market"),ResourcesHelper.GetString("ShellPage_Market_Desc"), typeof(MarketPage)),
                new ToolItem(ResourcesHelper.GetString("ShellPage_Business"),ResourcesHelper.GetString("ShellPage_Business_Desc"),typeof(BusinessPage)),
                new ToolItem(ResourcesHelper.GetString("ShellPage_Contract"), ResourcesHelper.GetString("ShellPage_Contract_Desc"), typeof(ContractPage)),
                new ToolItem(ResourcesHelper.GetString("ShellPage_ServerPing"), ResourcesHelper.GetString("ShellPage_ServerPing_Desc"), typeof(ServerPingPage)),
                new ToolItem(ResourcesHelper.GetString("ShellPage_Translation"), ResourcesHelper.GetString("ShellPage_Translation_Desc"), typeof(TranslationPage)),
                new ToolItem(ResourcesHelper.GetString("ShellPage_Wormhole"),ResourcesHelper.GetString("ShellPage_Wormhole_Desc"), typeof(WormholePage)),
                new ToolItem(ResourcesHelper.GetString("ShellPage_DED"), ResourcesHelper.GetString("ShellPage_DED_Desc"), typeof(DEDPage)),
                new ToolItem(ResourcesHelper.GetString("ShellPage_Backstory"),ResourcesHelper.GetString("ShellPage_Backstory_Desc"), typeof(BackstoryPage)),
                new ToolItem(ResourcesHelper.GetString("ShellPage_Mission"),ResourcesHelper.GetString("ShellPage_Mission_Desc"), typeof(MissionPage)),
                new ToolItem(ResourcesHelper.GetString("ShellPage_GameLogMonitor"),ResourcesHelper.GetString("ShellPage_GameLogMonitor_Desc"), typeof(GameLogMonitorPage)),
                new ToolItem(ResourcesHelper.GetString("ShellPage_ChannelMonitor"),ResourcesHelper.GetString("ShellPage_ChannelMonitor_Desc"), typeof(ChannelMonitorPage)),
                new ToolItem(ResourcesHelper.GetString("ShellPage_Links"),ResourcesHelper.GetString("ShellPage_Links_Desc"), typeof(LinksPage)),
                new ToolItem(ResourcesHelper.GetString("ShellPage_ZKB"),ResourcesHelper.GetString("ShellPage_ZKB_Desc"), typeof(ZKBHomePage)),
                new ToolItem(ResourcesHelper.GetString("ShellPage_Database"),ResourcesHelper.GetString("ShellPage_Database_Desc"), typeof(DatabasePage)),
                new ToolItem(ResourcesHelper.GetString("ShellPage_Setting"),"", typeof(SettingPage)),
            };
            GetServerStatus();
        }
        private static string GetVersionDescription()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }
        private CancellationTokenSource _cancellationTokenSource;
        private void GetServerStatus()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;
            var esi = ESIService.GetDefaultEsi();
            Task.Run(async() =>
            {
                while(!token.IsCancellationRequested)
                {
                    try
                    {
                        var resp = await esi.Status.Retrieve();
                        if(resp.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            Window?.DispatcherQueue.TryEnqueue(() =>
                            {
                                Players = resp.Data.Players.ToString("N0");
                            });
                        }
                        else
                        {
                            Window?.DispatcherQueue.TryEnqueue(() =>
                            {
                                Players = "-";
                            });
                        }
                        Thread.Sleep(10000);
                    }
                    catch(Exception ex)
                    {
                        Core.Log.Error(ex);
                    }
                }
            });
            
        }
        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
        }
    }
}
