using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Extensions;
using System.IO;
using Microsoft.UI.Xaml;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.Models.EVELogs;

namespace TheGuideToTheNewEden.WinUI.Models
{
    /// <summary>
    /// 一个角色频道预警实例
    /// </summary>
    internal class ChannelIntel : ObservableObject
    {
        private List<Core.DBModels.MapSolarSystemBase> _mapSolarSystems;
        private List<string> _nameDbs;
        private Core.Models.Map.IntelSolarSystemMap _intelMap { get; set; }
        private List<Core.Models.ChannelIntel.ChannelIntelObserver> _observers = new List<Core.Models.ChannelIntel.ChannelIntelObserver>();
        private Core.Models.ChannelIntel.ChannelIntelObserver _localObservers;
        private Core.Intel.ZKBIntel _zkbIntel;
        private Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;

        private MapSolarSystemBase _nullSolarSystem = new MapSolarSystemBase()
        {
            SolarSystemID = -1,
            SolarSystemName = string.Empty
        };

        private List<ChatChanelInfo> _chatChanelInfos;
        /// <summary>
        /// 当前角色所有聊天频道
        /// </summary>
        public List<ChatChanelInfo> ChatChanelInfos
        {
            get => _chatChanelInfos;
            set => SetProperty(ref _chatChanelInfos, value);
        }
        public readonly string Listener;
        public readonly Core.Models.ChannelIntel.ChannelIntelSetting Setting;

        private List<string> _selectedNameDbs;
        public List<string> SelectedNameDbs
        {
            get => _selectedNameDbs;
            set => SetProperty(ref _selectedNameDbs, value);
        }

        private MapSolarSystemBase _localSolarSystem;
        public MapSolarSystemBase LocalSolarSystem
        {
            get => _localSolarSystem;
            set => SetProperty(ref _localSolarSystem, value);
        }

        private MapSolarSystem _searchSolarSystem;
        public MapSolarSystem SearchSolarSystem
        {
            get => _searchSolarSystem;
            set 
            {
                if(SetProperty(ref _searchSolarSystem, value) && value != null)
                {
                    LocalSolarSystem = value;
                    Setting.LocationID = value.SolarSystemID;
                }
            }
        }

        private bool _running;
        public bool Running
        {
            get => _running;
            set => SetProperty(ref _running, value);
        }

        public ChannelIntel(string listener, List<ChatChanelInfo> chatChanelInfos, List<Core.DBModels.MapSolarSystemBase> mapSolarSystems, List<string> nameDbs,
            Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue)
        {
            Listener = listener;
            _chatChanelInfos = chatChanelInfos;
            _mapSolarSystems = mapSolarSystems;
            _nameDbs = nameDbs;
            _dispatcherQueue = dispatcherQueue;
            var setting = Services.Settings.IntelSettingService.GetValue(listener);
            if(setting == null)
            {
                setting = new Core.Models.ChannelIntel.ChannelIntelSetting();
                setting.Listener = listener;
            }
            FixSoundSetting(setting);
            Setting = setting;
            LoadSetting();
            Setting.PropertyChanged += Setting_PropertyChanged;
        }

        private void LoadSetting()
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
                ChatChanelInfos.ForEach(p => p.IsChecked = false);
            }

            if (Setting.LocationID > 1)
            {
                LocalSolarSystem = _mapSolarSystems.FirstOrDefault(p => p.SolarSystemID == Setting.LocationID);
            }
            else
            {
                LocalSolarSystem = _nullSolarSystem;
            }

            if (Setting.NameDbs.NotNullOrEmpty())
            {
                foreach (var db in Setting.NameDbs)
                {
                    var target = _nameDbs.FirstOrDefault(p => p == db);
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
                        _nameDbs.FirstOrDefault()
                    };
            }

            if (Setting.AutoUpdateLocaltion)
            {
                UpdateCharacterLocation();
            }
        }
        private static void FixSoundSetting(Core.Models.ChannelIntel.ChannelIntelSetting setting)
        {
            if (setting != null)
            {
                int diff = setting.IntelJumps + 1 - setting.Sounds.Count;
                if (diff != 0)
                {
                    if (diff < 0)
                    {
                        for (int i = 0; i < -diff; i++)
                        {
                            setting.Sounds.RemoveAt(setting.Sounds.Count - 1);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < diff; i++)
                        {
                            setting.Sounds.Add(new Core.Models.ChannelIntel.ChannelIntelSoundSetting()
                            {
                                Id = setting.Sounds.Count
                            });
                        }
                    }
                }

            }
        }
        private void SaveSetting()
        {
            Setting.ChannelIDs = ChatChanelInfos.Where(p => p.IsChecked).Select(p => p.ChannelID).ToList();
            //Setting.LocationID = LocalSolarSystem == null ? -1 : LocalSolarSystem.SolarSystemID;
            Setting.NameDbs = SelectedNameDbs.ToList();
            Services.Settings.IntelSettingService.SetValue(Setting);
        }
        private void Setting_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch(e.PropertyName)
            {
                case nameof(Core.Models.EarlyWarningSetting.AutoUpdateLocaltion):
                    {
                        //如果勾选上自动更新位置，立即获取当前位置
                        if (Setting.AutoUpdateLocaltion)
                        {
                            UpdateCharacterLocation();
                        }
                    }
                    break;
                case nameof(Core.Models.EarlyWarningSetting.IntelJumps):
                    {
                        if (!double.IsNaN(Setting.IntelJumps))
                        {
                            int diff = (int)(Setting.IntelJumps - Setting.Sounds.Count + 1);
                            if (diff < 0)
                            {
                                for (int i = 0; i < -diff; i++)
                                {
                                    Setting.Sounds.RemoveAt(Setting.Sounds.Count - 1);
                                }
                            }
                            else
                            {
                                for (int i = 0; i < diff; i++)
                                {
                                    Setting.Sounds.Add(new Core.Models.ChannelIntel.ChannelIntelSoundSetting()
                                    {
                                        Id = Setting.Sounds.Count
                                    });
                                }
                            }
                        }
                    }break;
                default:break;
            }
        }
        private async void UpdateCharacterLocation()
        {
            var localChat = ChatChanelInfos.FirstOrDefault(p => p.ChannelID == "local");
            if (localChat != null)
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
                    for (int i = 0; i < newLines.Count; i++)
                    {
                        var chatContent = Core.Models.EVELogs.IntelChatContent.Create(newLines[i]);
                        if (chatContent != null)
                        {
                            int id = await Core.EVEHelpers.ChatLogHelper.TryGetCharacterLocationAsync(chatContent, _nameDbs);
                            if (id != -1)
                            {
                                systemId = id;
                            }
                        }
                    }
                    if (systemId != -1)
                    {
                        LocalSolarSystem = _mapSolarSystems.FirstOrDefault(p => p.SolarSystemID == systemId);
                        Setting.LocationID = systemId;
                    }
                }
            }
        }
        private async Task<Dictionary<string, int>> GetSolarSystemNames()
        {
            if (SelectedNameDbs != null)
            {
                Dictionary<string, int> names = new Dictionary<string, int>();
                foreach (var item in SelectedNameDbs)
                {
                    var result = await Core.Services.DB.MapSolarSystemNameService.QueryAllAsync(item == _nameDbs.FirstOrDefault() ? Core.Config.DBPath : item);
                    if (result.NotNullOrEmpty())
                    {
                        foreach (var solar in result)
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
        public void UpdateChannels(List<ChatChanelInfo> chatChanelInfos)
        {
            ChatChanelInfos = chatChanelInfos;
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
                ChatChanelInfos.ForEach(p => p.IsChecked = false);
            }
        }

        public async Task Start()
        {
            if (Setting.LocationID <= 0)
            {
                throw new Exception("请设置角色当前所处星系");
            }
            if(LocalSolarSystem.IsSpecial())
            {
                throw new Exception("当前星系不支持频道预警");
            }
            if (ChatChanelInfos.NotNullOrEmpty())
            {
                _observers.Clear();
                //Setting.LocationID = LocalSolarSystem.SolarSystemID;
                var checkedItems = ChatChanelInfos.Where(p => p.IsChecked).ToList();
                if (checkedItems.NotNullOrEmpty())
                {
                    _intelMap = await Core.EVEHelpers.SolarSystemPosHelper.GetIntelSolarSystemMapAsync(Setting.LocationID, Setting.IntelJumps);
                    Core.EVEHelpers.SolarSystemPosHelper.ResetXY(_intelMap.GetAllSolarSystem());
                    foreach (var ch in checkedItems)
                    {
                        Core.Models.ChannelIntel.ChannelIntelObserver observer = new Core.Models.ChannelIntel.ChannelIntelObserver(ch, Setting);
                        observer.IntelMap = _intelMap;
                        if (Core.Services.ObservableFileService.Add(observer))
                        {
                            observer.SolarSystemNames = await GetSolarSystemNames();
                            observer.OnContentUpdate += Observer_OnContentUpdate;
                            observer.OnWarningUpdate += Observer_OnWarningUpdate;
                            _observers.Add(observer);

                            if (Setting.AutoUpdateLocaltion && ch.ChannelID == "local")//自动更新位置需要添加本地频道监控
                            {
                                observer.OnContentUpdate += Observer_LocalChanged;
                                _localObservers = observer;
                            }
                        }
                    }
                    if (Setting.AutoUpdateLocaltion && _localObservers == null)//自动更新位置需要添加本地频道监控
                    {
                        var localChat = ChatChanelInfos.FirstOrDefault(p => p.ChannelID == "local");
                        if (localChat != null)
                        {
                            Core.Models.ChannelIntel.ChannelIntelObserver observer = new Core.Models.ChannelIntel.ChannelIntelObserver(localChat, Setting);
                            if (Core.Services.ObservableFileService.Add(observer))
                            {
                                observer.SolarSystemNames = await GetSolarSystemNames();
                                observer.OnContentUpdate += Observer_LocalChanged;
                                _observers.Add(observer);
                                _localObservers = observer;
                            }
                        }
                    }
                    if (Setting.SubZKB)
                    {
                        _zkbIntel = new Core.Intel.ZKBIntel(Setting, _intelMap);
                        await _zkbIntel.Start();
                        _zkbIntel.OnWarningUpdate += ZkbIntel_OnWarningUpdate;
                    }
                    Services.WarningService.Current.Add(Setting, _intelMap);
                    var intelWindow = Services.WarningService.Current.GetIntelWindow(Setting.Listener);
                    if (intelWindow != null)
                    {
                        intelWindow.OnStop += IntelWindow_OnStop;
                    }
                    Running = true;
                    SaveSetting();
                }
                else
                {
                    throw new Exception("请选择预警频道");
                }
            }
            SaveSetting();
        }

        private void Observer_OnWarningUpdate(Core.Models.ChannelIntel.ChannelIntelObserver channelIntelObserver, IEnumerable<Core.Models.EarlyWarningContent> news)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                foreach (var ch in news)
                {
                    if (ch.IntelType == Core.Enums.IntelChatType.Intel)
                    {
                        Core.Models.ChannelIntel.ChannelIntelSoundSetting soundSetting = null;
                        if (Setting.Sounds.Count >= ch.Jumps)
                        {
                            soundSetting = Setting.Sounds[ch.Jumps];
                        }
                        WarningService.Current.Notify(channelIntelObserver.ChatChanelInfo.Listener, soundSetting, Setting.SystemNotify, channelIntelObserver.ChatChanelInfo.ChannelName, ch);
                    }
                    else
                    {
                        //只clr
                        WarningService.Current.GetIntelWindow(channelIntelObserver.ChatChanelInfo.Listener)?.Intel(ch);
                    }
                }
            });
        }
        public event EventHandler<IEnumerable<Core.Models.EVELogs.IntelChatContent>> ChatContentEvent;
        private void Observer_OnContentUpdate(Core.Models.ChannelIntel.ChannelIntelObserver channelIntelObserver, IEnumerable<Core.Models.EVELogs.IntelChatContent> news)
        {
            ChatContentEvent?.Invoke(this, news);
        }
        private async void Observer_LocalChanged(Core.Models.ChannelIntel.ChannelIntelObserver channelIntelObserver, IEnumerable<Core.Models.EVELogs.IntelChatContent> news)
        {
            for (int i = news.Count() - 1; i >= 0; i--)//从后面往回找，新消息位于后面
            {
                var id = await Core.EVEHelpers.ChatLogHelper.TryGetCharacterLocationAsync(news.ElementAt(i), _nameDbs);
                if (id > 0)
                {
                    _dispatcherQueue.TryEnqueue(async () =>
                    {
                        Setting.LocationID = id;
                        //LocalSolarSystem = _mapSolarSystems.FirstOrDefault(p => p.SolarSystemID == id);
                        _intelMap = await Core.EVEHelpers.SolarSystemPosHelper.GetIntelSolarSystemMapAsync(Setting.LocationID, Setting.IntelJumps);
                        Core.EVEHelpers.SolarSystemPosHelper.ResetXY(_intelMap.GetAllSolarSystem());
                        foreach (var item in _observers)
                        {
                            item.IntelMap = _intelMap;
                        }
                        WarningService.Current.UpdateWindowHome(Setting.Listener, _intelMap);
                    });
                    break;
                }
            }
        }
        public event EventHandler<Core.Models.EarlyWarningContent> ZKBIntelEvent;
        private void ZkbIntel_OnWarningUpdate(object sender, Core.Models.EarlyWarningContent e)
        {
            ZKBIntelEvent?.Invoke(this, e);
            _dispatcherQueue.TryEnqueue(() =>
            {
                var span = DateTime.UtcNow - e.Time;
                string desc;
                if (span.TotalMinutes > 1)
                {
                    desc = $" {span.TotalMinutes.ToString("N1")}{Helpers.ResourcesHelper.GetString("EarlyWarningPage_Befor_Min")}";
                }
                else
                {
                    desc = $" {span.TotalSeconds.ToString("N0")}{Helpers.ResourcesHelper.GetString("EarlyWarningPage_Befor_Sec")}";
                }
                e.Content += desc;
                Core.Models.ChannelIntel.ChannelIntelSoundSetting soundSetting = null;
                if (Setting.Sounds.Count >= e.Jumps)
                {
                    soundSetting = Setting.Sounds[e.Jumps];
                }
                WarningService.Current.Notify((sender as Core.Intel.ZKBIntel).GetListener(), soundSetting, Setting.SystemNotify, "KB", e);
            });
        }
        private void IntelWindow_OnStop()
        {
            Stop();
        }
        public void Stop()
        {
            Core.Services.ObservableFileService.Remove(_observers);
            _observers.Clear();
            Running = false;
            _localObservers = null;
            _zkbIntel?.Stop();
            Services.WarningService.Current.Remove(Setting?.Listener);
            GC.Collect();
        }
        public void StopSound()
        {
            if (!string.IsNullOrEmpty(Setting?.Listener))
            {
                WarningService.Current.StopSound(Setting.Listener);
            }
        }
        public bool RestorePos()
        {
            if (!string.IsNullOrEmpty(Setting?.Listener))
            {
                return WarningService.Current.RestoreWindowPos(Setting.Listener);
            }
            else
            {
                return false;
            }
        }
    }
}
