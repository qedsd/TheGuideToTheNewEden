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
        public static HashSet<string> RunningCharacters;
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

        private ObservableCollection<IntelChatContent> chatContents = new ObservableCollection<IntelChatContent>();
        /// <summary>
        /// 当前选中频道所有聊天内容
        /// </summary>
        public ObservableCollection<IntelChatContent> ChatContents
        {
            get => chatContents;
            set => SetProperty(ref chatContents, value);
        }

        /// <summary>
        /// 当前选中的聊天频道对应的预警项
        /// </summary>
        private List<Core.Models.EarlyWarningItem> EarlyWarningItems = new List<Core.Models.EarlyWarningItem>();
        private Core.Models.EarlyWarningItem LocalEarlyWarningItem;

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

        private List<string> selectedNameDbs;
        public List<string> SelectedNameDbs
        {
            get => selectedNameDbs;
            set => SetProperty(ref selectedNameDbs, value);
        }

        private List<Core.DBModels.MapSolarSystemBase> mapSolarSystems;
        public List<Core.DBModels.MapSolarSystemBase> MapSolarSystems
        {
            get => mapSolarSystems;
            set => SetProperty(ref mapSolarSystems, value);
        }

        private List<Core.DBModels.MapSolarSystemBase> searchMapSolarSystems;
        public List<Core.DBModels.MapSolarSystemBase> SearchMapSolarSystems
        {
            get => searchMapSolarSystems;
            set => SetProperty(ref searchMapSolarSystems, value);
        }

        private string locationSolarSystem;
        public string LocationSolarSystem { get => locationSolarSystem; set => SetProperty(ref locationSolarSystem, value); }

        private Core.DBModels.MapSolarSystemBase selectedMapSolarSystem;
        public Core.DBModels.MapSolarSystemBase SelectedMapSolarSystem
        {
            get => selectedMapSolarSystem;
            set
            {
                SetProperty(ref selectedMapSolarSystem, value);
                LocationSolarSystem = value?.SolarSystemName;
            }
        }
        private Core.Models.EarlyWarningSetting setting = new Core.Models.EarlyWarningSetting();
        public Core.Models.EarlyWarningSetting Setting
        {
            get => setting;
            set => SetProperty(ref setting, value);
        }
        public Core.Models.Map.IntelSolarSystemMap IntelMap { get; set; }
        internal EarlyWarningItemViewModel()
        {
            LogPath = System.IO.Path.Combine(EVELogsPathSelectorService.Value, "Chatlogs");
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
                    var dic = GameLogHelper.GetChatChanelInfos(LogPath);
                    if(dic != null)
                    {
                        foreach(var item in dic)
                        {
                            List<ChatChanelInfo> chatChanelInfos = new List<ChatChanelInfo>();
                            foreach(var coreChatChanelInfos in item.Value)
                            {
                                chatChanelInfos.Add(ChatChanelInfo.Create(coreChatChanelInfos));
                            }
                            ListenerChannelDic.Add(item.Key, chatChanelInfos);
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
            }
            OnSelectedCharacterChanged?.Invoke(selectedCharacter);
            var setting = Services.Settings.IntelSettingService.GetValue(selectedCharacter);
            if(setting == null)
            {
                Setting = Setting.DepthClone<Core.Models.EarlyWarningSetting>();
                Setting.Listener = SelectedCharacter;
            }
            else
            {
                Setting = setting;
            }
            LoadSetting();
            if(Setting.AutoUpdateLocaltion)
            {
                UpdateCharacterLocation();
            }
            Setting.PropertyChanged += Setting_PropertyChanged;
        }

        /// <summary>
        /// 设置参数变化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Setting_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName == nameof(Core.Models.EarlyWarningSetting.AutoUpdateLocaltion))
            {
                //如果勾选上自动更新位置，立即获取当前位置
                if(Setting.AutoUpdateLocaltion)
                {
                    UpdateCharacterLocation();
                }
            }
        }

        public ICommand PickLogFolderCommand => new RelayCommand(async() =>
        {
            var folder = await Helpers.PickHelper.PickFolderAsync(Helpers.WindowHelper.CurrentWindow());
            if(folder != null)
            {
                LogPath = folder.Path;
            }
        });
        public ICommand PickSoundFileCommand => new RelayCommand(async () =>
        {
            var file = await Helpers.PickHelper.PickFileAsync(Window);
            if (file != null)
            {
                Setting.SoundFilePath= file.Path;
            }
        });

        public ICommand StartCommand => new RelayCommand(async() =>
        {
            if(SelectedCharacter == null)
            {
                Window.ShowError("请选择角色");
                return;
            }
            if(RunningCharacters.Contains(SelectedCharacter))
            {
                Window.ShowError("此角色已开启预警");
                return;
            }
            if(string.IsNullOrEmpty(LocationSolarSystem))
            {
                Window.ShowError("请设置角色当前所处星系");
                return;
            }
            var location = MapSolarSystems.FirstOrDefault(p => p.SolarSystemName == LocationSolarSystem);
            if (location == null)
            {
                Window.ShowError("无效的星系名称");
                HideWaiting();
                return;
            }
            ShowWaiting();
            if(ChatChanelInfos.NotNullOrEmpty())
            {
                SelectedMapSolarSystem = location;
                Setting.LocationID = location.SolarSystemID;
                var checkedItems = ChatChanelInfos.Where(p => p.IsChecked).ToList();
                if(checkedItems.NotNullOrEmpty())
                {
                    IntelMap = await Core.EVEHelpers.SolarSystemPosHelper.GetIntelSolarSystemMapAsync(Setting.LocationID, Setting.IntelJumps);
                    Core.EVEHelpers.SolarSystemPosHelper.ResetXY(IntelMap.GetAllSolarSystem());
                    foreach (var ch in checkedItems)
                    {
                        Core.Models.EarlyWarningItem earlyWarningItem = new Core.Models.EarlyWarningItem(ch,setting);
                        earlyWarningItem.IntelMap = IntelMap;
                        if (Core.Services.ObservableFileService.Add(earlyWarningItem))
                        {
                            earlyWarningItem.SolarSystemNames = await GetSolarSystemNames();
                            earlyWarningItem.OnContentUpdate += EarlyWarningItem_OnContentUpdate;
                            earlyWarningItem.OnWarningUpdate += EarlyWarningItem_OnWarningUpdate;
                            EarlyWarningItems.Add(earlyWarningItem);

                            if(Setting.AutoUpdateLocaltion && ch.ChannelID == "local")//自动更新位置需要添加本地频道监控
                            {
                                earlyWarningItem.OnContentUpdate += EarlyWarningItem_LocalChanged;
                                LocalEarlyWarningItem = earlyWarningItem;
                            }
                        }
                    }
                    if(Setting.AutoUpdateLocaltion && LocalEarlyWarningItem == null)//自动更新位置需要添加本地频道监控
                    {
                        var localChat = ChatChanelInfos.FirstOrDefault(p => p.ChannelID == "local");
                        if(localChat != null)
                        {
                            Core.Models.EarlyWarningItem earlyWarningItem = new Core.Models.EarlyWarningItem(localChat, setting);
                            if (Core.Services.ObservableFileService.Add(earlyWarningItem))
                            {
                                earlyWarningItem.SolarSystemNames = await GetSolarSystemNames();
                                earlyWarningItem.OnContentUpdate += EarlyWarningItem_LocalChanged;
                                EarlyWarningItems.Add(earlyWarningItem);
                                LocalEarlyWarningItem = earlyWarningItem;
                            }
                        }
                    }
                    if(Setting.OverlapType != 2)
                    {
                        var intelWindow = WarningService.AddNotifyWindow(Setting, IntelMap);
                        if(intelWindow != null)
                        {
                            intelWindow.OnStop += IntelWindow_OnStop;
                            if (Setting.OverlapType == 0)
                            {
                                WarningService.ShowWindow(Setting.Listener);
                            }
                        }
                        else
                        {
                            ShowError("开启预警窗口失败");
                        }
                    }
                    IsRunning = true;
                    RunningCharacters.Add(SelectedCharacter);
                    SaveSetting();
                }
                else
                {
                    ShowMsg("请选择预警频道");
                }
            }
            HideWaiting();
        });

        private void IntelWindow_OnStop()
        {
            StopCommand.Execute(null);
        }

        public ICommand StopCommand => new RelayCommand(() =>
        {
            Core.Services.ObservableFileService.Remove(EarlyWarningItems);
            EarlyWarningItems.Clear();
            ChatContents.Clear();
            IsRunning = false;
            LocalEarlyWarningItem = null;
            Services.WarningService.RemoveWindow(Setting.Listener);
            RunningCharacters.Remove(SelectedCharacter);
            GC.Collect();
        });

        public ICommand RestorePosCommand => new RelayCommand(() =>
        {
            if(!string.IsNullOrEmpty(Setting?.Listener))
            {
                if(WarningService.RestoreWindowPos(Setting.Listener))
                {
                    Window?.ShowSuccess("重置成功");
                }
                else
                {
                    Window?.ShowError("重置失败");
                }
            }
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
                if(Setting.OverlapType != 2)
                {
                    Window.DispatcherQueue.TryEnqueue(() =>
                    {
                        WarningService.NotifyWindow(Setting.Listener, ch);
                    });
                }
                if(ch.IntelType == Core.Enums.IntelChatType.Intel)
                {
                    if (Setting.SystemNotify)
                    {
                        WarningService.NotifyToast(ch);
                    }
                    if (Setting.MakeSound)
                    {
                        WarningService.NotifySound(Setting.SoundFilePath);
                    }
                }
            }
        }
        /// <summary>
        /// 频道内容更新
        /// </summary>
        /// <param name="earlyWarningItem"></param>
        /// <param name="newlines"></param>
        private void EarlyWarningItem_OnContentUpdate(Core.Models.EarlyWarningItem earlyWarningItem, IEnumerable<IntelChatContent> news)
        {
            Helpers.WindowHelper.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                foreach (var line in news)
                {
                    ChatContents.Add(line);
                }
            });
        }
        /// <summary>
        /// 本地频道内容更新
        /// 判断是否更新位置
        /// </summary>
        /// <param name="earlyWarningItem"></param>
        /// <param name="news"></param>
        private async void EarlyWarningItem_LocalChanged(Core.Models.EarlyWarningItem earlyWarningItem, IEnumerable<IntelChatContent> news)
        {
            for(int i = news.Count() - 1;i >= 0; i--)//从后面往回找，新消息位于后面
            {
                var id = await Core.EVEHelpers.ChatLogHelper.TryGetCharacterLocationAsync(news.ElementAt(i),NameDbs);
                if(id > 0)
                {
                    Window.DispatcherQueue.TryEnqueue(async() =>
                    {
                        Setting.LocationID = id;
                        SelectedMapSolarSystem = MapSolarSystems.FirstOrDefault(p => p.SolarSystemID == id);
                        IntelMap = await Core.EVEHelpers.SolarSystemPosHelper.GetIntelSolarSystemMapAsync(Setting.LocationID, Setting.IntelJumps);
                        Core.EVEHelpers.SolarSystemPosHelper.ResetXY(IntelMap.GetAllSolarSystem());
                        foreach(var item in EarlyWarningItems)
                        {
                            item.IntelMap = IntelMap;
                        }
                        WarningService.UpdateWindowHome(Setting.Listener, IntelMap);
                    });
                    break;
                }
            }
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
                    int systemId = -1;
                    for(int i = 0; i < newLines.Count; i++)
                    {
                        var chatContent = IntelChatContent.Create(newLines[i]);
                        if(chatContent != null)
                        {
                            int id = await Core.EVEHelpers.ChatLogHelper.TryGetCharacterLocationAsync(chatContent, NameDbs);
                            if(id != -1)
                            {
                                systemId = id;
                            }
                        }
                    }
                    if (systemId != -1)
                    {
                        SelectedMapSolarSystem = MapSolarSystems.FirstOrDefault(p => p.SolarSystemID == systemId);
                        Setting.LocationID = systemId;
                    }
                }
            }
        }
        

        /// <summary>
        /// 加载应用设置
        /// </summary>
        private void LoadSetting()
        {
            if(Setting != null)
            {
                if (Setting.ChannelIDs.NotNullOrEmpty())
                {
                    foreach (var id in Setting.ChannelIDs)
                    {
                        var target = ChatChanelInfos.FirstOrDefault(p => p.ChannelID == id);
                        if (target != null)
                        {
                            target.IsChecked = true;
                        }
                    }
                }
                else
                {
                    ChatChanelInfos.ForEach(p=>p.IsChecked = false);
                }
                if(Setting.LocationID > 1)
                {
                    SelectedMapSolarSystem = MapSolarSystems.FirstOrDefault(p=>p.SolarSystemID == Setting.LocationID);
                }
                else
                {
                    SelectedMapSolarSystem = null;
                }
                if (Setting.NameDbs.NotNullOrEmpty())
                {
                    foreach (var db in Setting.NameDbs)
                    {
                        var target = NameDbs.FirstOrDefault(p => p == db);
                        if (target != null)
                        {
                            if (SelectedNameDbs == null)
                            {
                                SelectedNameDbs = new List<string>();
                            }
                            SelectedNameDbs.Add(target);
                        }
                    }
                }
                else
                {
                    SelectedNameDbs = new List<string>()
                    {
                        NameDbs.FirstOrDefault()
                    };
                }
            }
        }

        private void SaveSetting()
        {
            if (Setting != null)
            {
                Setting.ChannelIDs = ChatChanelInfos.Where(p => p.IsChecked).Select(p=>p.ChannelID).ToList();
                Setting.LocationID = SelectedMapSolarSystem == null ? -1 : SelectedMapSolarSystem.SolarSystemID;
                Setting.NameDbs = SelectedNameDbs.ToList();
                IntelSettingService.SetValue(Setting);
            }
        }
    }
}
