using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Shapes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WinUI.Models;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using Windows.UI.ViewManagement;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class EarlyWarningItemViewModel : BaseViewModel
    {
        private string logPath;
        public string LogPath
        {
            get => logPath;
            set
            {
                SetProperty(ref logPath, value);
                InitDicAsync();
            }
        }

        /// <summary>
        /// key为角色名
        /// value为角色下所有的不重复频道
        /// 每个频道都是该频道最新日期的那个文件
        /// </summary>
        private Dictionary<string, List<ChatChanelInfo>> ListenerChannelDic;

        private string selectedCharacter;
        public string SelectedCharacter
        {
            get => selectedCharacter;
            set
            {
                SetProperty(ref selectedCharacter, value);
                UpdateSelectedCharacter();
            }
        }

        private List<string> characters;
        public List<string> Characters
        {
            get => characters;
            set => SetProperty(ref characters, value);
        }

        private List<ChatChanelInfo> chatChanelInfos;
        /// <summary>
        /// 当前角色所有聊天频道
        /// </summary>
        public List<ChatChanelInfo> ChatChanelInfos
        {
            get => chatChanelInfos;
            set => SetProperty(ref chatChanelInfos, value);
        }

        private ObservableCollection<ChatContent> chatContents = new ObservableCollection<ChatContent>();
        /// <summary>
        /// 当前选中频道所有聊天内容
        /// </summary>
        public ObservableCollection<ChatContent> ChatContents
        {
            get => chatContents;
            set => SetProperty(ref chatContents, value);
        }

        /// <summary>
        /// 当前选中的聊天频道对应的预警项
        /// </summary>
        private List<Core.Models.EarlyWarningItem> EarlyWarningItems = new List<Core.Models.EarlyWarningItem>();

        private bool isRunning;
        /// <summary>
        /// 是否监控中
        /// </summary>
        public bool IsRunning
        {
            get => isRunning;
            set => SetProperty(ref isRunning, value);
        }

        private List<string> nameDbs;
        public List<string> NameDbs
        {
            get => nameDbs;
            set => SetProperty(ref nameDbs, value);
        }

        public List<string> SelectedNameDbs { get; set; }

        internal EarlyWarningItemViewModel()
        {
            LogPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EVE", "logs", "Chatlogs");
            InitNameDbs();
        }
        private void InitNameDbs()
        {
            NameDbs = new List<string>()
            {
                "default(en)"
            };
            var local = LocalDbSelectorService.GetAll();
            if(local.NotNullOrEmpty())
            {
                NameDbs.AddRange(local);
            }
        }
        private async void InitDicAsync()
        {
            ListenerChannelDic = new Dictionary<string, List<ChatChanelInfo>>();
            if (System.IO.Directory.Exists(LogPath))
            {
                await Task.Run(() =>
                {
                    var allFiles = System.IO.Directory.GetFiles(LogPath);
                    Dictionary<string, Core.Models.ChatChanelInfo> onlyOneChanels = new Dictionary<string, Core.Models.ChatChanelInfo>();
                    foreach (var file in allFiles)
                    {
                        var chanelInfo = GameLogHelper.GetChatChanelInfo(file);
                        if (chanelInfo != null)
                        {
                            if (ListenerChannelDic.TryGetValue(chanelInfo.Listener, out List<ChatChanelInfo> channels))
                            {
                                //频道会按每天单独存储为一个文件，需要监控最新日期的那个
                                var sameChanel = channels.FirstOrDefault(p => p.ChannelName == chanelInfo.ChannelName);
                                if(sameChanel != null && sameChanel.SessionStarted < chanelInfo.SessionStarted)
                                {
                                    channels.Remove(sameChanel);
                                }
                                channels.Add(ChatChanelInfo.Create(chanelInfo));
                            }
                            else
                            {
                                channels = new List<ChatChanelInfo>()
                                {
                                    ChatChanelInfo.Create(chanelInfo)
                                };
                                ListenerChannelDic.Add(chanelInfo.Listener, channels);
                            }
                        }
                    }
                });
            }
            if (ListenerChannelDic.Count != 0)
            {
                Characters = ListenerChannelDic.Select(p => p.Key).ToList();
            }
            else
            {
                Characters = null;
            }
        }

        internal delegate void SelectedCharacterChanged(string selectedCharacter);
        internal event SelectedCharacterChanged OnSelectedCharacterChanged;
        private void UpdateSelectedCharacter()
        {
            if(ListenerChannelDic.TryGetValue(selectedCharacter,out var chatChanelInfos))
            {
                ChatChanelInfos = chatChanelInfos;
                GetCharacterLocation();
            }
            OnSelectedCharacterChanged?.Invoke(selectedCharacter);
        }

        public ICommand PickLogFolderCommand => new RelayCommand(async() =>
        {
            var folder = await Helpers.PickHelper.PickFolderAsync(Helpers.WindowHelper.CurrentWindow());
            if(folder != null)
            {
                LogPath = folder.Path;
            }
        });

        public ICommand StartCommand => new RelayCommand(async() =>
        {
            if(ChatChanelInfos.NotNullOrEmpty())
            {
                var checkedItems = ChatChanelInfos.Where(p => p.IsChecked).ToList();
                if(checkedItems.NotNullOrEmpty())
                {
                    foreach (var ch in checkedItems)
                    {
                        Core.Models.EarlyWarningItem earlyWarningItem = new Core.Models.EarlyWarningItem(ch);
                        earlyWarningItem.SolarSystemNames = await GetSolarSystemNames();
                        earlyWarningItem.OnContentUpdate += EarlyWarningItem_OnContentUpdate;
                        earlyWarningItem.OnWarningUpdate += EarlyWarningItem_OnWarningUpdate;
                        if (Core.Services.ObservableFileService.Add(earlyWarningItem))
                        {
                            EarlyWarningItems.Add(earlyWarningItem);
                            earlyWarningItem.Update();
                        }
                    }
                    IsRunning = true;
                }
            }
        });

        public ICommand StopCommand => new RelayCommand(() =>
        {
            Core.Services.ObservableFileService.Remove(EarlyWarningItems);
            EarlyWarningItems.Clear();
            ChatContents.Clear();
            IsRunning = false;
        });

        /// <summary>
        /// 预警更新
        /// </summary>
        /// <param name="earlyWarningItem"></param>
        /// <param name="news"></param>
        private void EarlyWarningItem_OnWarningUpdate(Core.Models.EarlyWarningItem earlyWarningItem, IEnumerable<Core.Models.EarlyWarningContent> news)
        {
            foreach(var ch in news)
            {
                WarningService.NotifyWindow(ch);
                WarningService.NotifyPopupToast(ch);
            }
        }
        /// <summary>
        /// 频道内容更新
        /// </summary>
        /// <param name="earlyWarningItem"></param>
        /// <param name="newlines"></param>
        private void EarlyWarningItem_OnContentUpdate(Core.Models.EarlyWarningItem earlyWarningItem, IEnumerable<ChatContent> news)
        {
            Helpers.WindowHelper.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                foreach (var line in news)
                {
                    ChatContents.Add(line);
                }
            });
        }

        private async Task<Dictionary<string,int>> GetSolarSystemNames()
        {
            if(SelectedNameDbs != null)
            {
                Dictionary<string, int> names = new Dictionary<string, int>();
                foreach (var item in SelectedNameDbs)
                {
                    var result = await MapSolarSystemNameService.QueryAllAsync(item == NameDbs.FirstOrDefault() ? Core.Config.DBPath : item);
                    if(result.NotNullOrEmpty())
                    {
                        foreach(var solar in result)
                        {
                            names.TryAdd(solar.SolarSystemName, solar.SolarSystemID);
                        }
                    }
                }
                return names;
            }
            else
            {
                return null;
            }
        }

        private void GetCharacterLocation()
        {
            var localChat = ChatChanelInfos.FirstOrDefault(p => p.ChannelID == "local");
            if(localChat != null)
            {
            }
        }
    }
}
