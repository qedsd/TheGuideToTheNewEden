using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.UI.Converters;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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

        private List<Core.DBModels.MapSolarSystemBase> mapSolarSystems;
        public List<Core.DBModels.MapSolarSystemBase> MapSolarSystems
        {
            get => mapSolarSystems;
            set => SetProperty(ref mapSolarSystems, value);
        }

        private Core.DBModels.MapSolarSystemBase selectedMapSolarSystem;
        public Core.DBModels.MapSolarSystemBase SelectedMapSolarSystem
        {
            get => selectedMapSolarSystem;
            set => SetProperty(ref selectedMapSolarSystem, value);
        }

        internal EarlyWarningItemViewModel()
        {
            LogPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EVE", "logs", "Chatlogs");
            InitNameDbs();
            InitSolarSystems();
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
        private async void InitSolarSystems()
        {
            var list = await Core.Services.DB.MapSolarSystemService.QueryAllAsync();
            if(list.NotNullOrEmpty())
            {
                MapSolarSystems = list.Select(p=>p as Core.DBModels.MapSolarSystemBase).ToList();
            }
        }

        internal delegate void SelectedCharacterChanged(string selectedCharacter);
        internal event SelectedCharacterChanged OnSelectedCharacterChanged;
        private void UpdateSelectedCharacter()
        {
            if(ListenerChannelDic.TryGetValue(selectedCharacter,out var chatChanelInfos))
            {
                ChatChanelInfos = chatChanelInfos;
                UpdateCharacterLocation();
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

        private async void UpdateCharacterLocation()
        {
            var localChat = ChatChanelInfos.FirstOrDefault(p => p.ChannelID == "local");
            if(localChat != null)
            {
                List<string> newLines = null;
                using (FileStream fs = new FileStream(localChat.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] b = new byte[1024];
                    int curReadCount = 0;
                    StringBuilder stringBuilder = new StringBuilder();
                    while ((curReadCount = fs.Read(b, 0, b.Length)) > 0)
                    {
                        var content = Encoding.Unicode.GetString(b, 0, curReadCount);
                        stringBuilder.Append(content);
                    }
                    if (stringBuilder.Length > 0)
                    {
                        newLines = stringBuilder.ToString().Split(new char[] { '\n', '\r' }).ToList();
                    }
                }
                if (newLines.NotNullOrEmpty())
                {
                    for(int i = newLines.Count - 1; i >= 0; i--)
                    {
                        var chatContent = ChatContent.Create(newLines[i]);
                        if(chatContent != null)
                        {
                            int id = await TryGetCharacterLocationAsync(chatContent);
                            if(id != -1)
                            {
                                //Helpers.WindowHelper.MainWindow.DispatcherQueue.TryEnqueue(() =>
                                //{
                                    SelectedMapSolarSystem = MapSolarSystems.FirstOrDefault(p => p.SolarSystemID == id);
                                //});
                            }
                        }
                    }
                }
            }
        }
        private async Task<int> TryGetCharacterLocationAsync(ChatContent chatContent)
        {
            if (Core.EVEHelpers.ChatSpeakerHelper.IsEVESystem(chatContent.SpeakerName))
            {
                if (Core.EVEHelpers.ChatSystemContentFormatHelper.IsLocalChanged(chatContent.Content))
                {
                    var array = chatContent.Content.Split(new char[] { ':', '：' });
                    if (array.Length > 0)
                    {
                        var name = array.Last().Trim().Replace("*", "");
                        foreach (var db in NameDbs)
                        {
                            int id = await MapSolarSystemNameService.QueryIdAsync(db == NameDbs.FirstOrDefault() ? Core.Config.DBPath : db, name);
                            if(id != -1)
                            {
                                return id;
                            }
                        }
                    }
                }
            }
            return -1;
        }
    }
}
