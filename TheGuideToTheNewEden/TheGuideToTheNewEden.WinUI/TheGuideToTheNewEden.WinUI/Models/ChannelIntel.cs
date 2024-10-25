using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Extensions;
using System.IO;
using Microsoft.UI.Xaml;

namespace TheGuideToTheNewEden.WinUI.Models
{
    /// <summary>
    /// 一个角色频道预警实例
    /// </summary>
    internal class ChannelIntel:ObservableObject
    {
        private List<ChatChanelInfo> _chatChanelInfos;
        private List<Core.DBModels.MapSolarSystemBase> _mapSolarSystems;
        private List<string> _nameDbs;
        private Core.Models.Map.IntelSolarSystemMap _intelMap { get; set; }
        private List<Core.Models.ChannelIntel.ChannelIntelObserver> _observers = new List<Core.Models.ChannelIntel.ChannelIntelObserver>();
        private Core.Models.ChannelIntel.ChannelIntelObserver _localObservers;
        private Core.Intel.ZKBIntel _zkbIntel;

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

        private string locationSolarSystem;
        public string LocationSolarSystem
        { 
            get => locationSolarSystem; 
            set => SetProperty(ref locationSolarSystem, value);
        }

        private bool _running;
        public bool Running
        {
            get => _running;
            set => SetProperty(ref _running, value);
        }

        public ChannelIntel(string listener, List<ChatChanelInfo> chatChanelInfos, List<Core.DBModels.MapSolarSystemBase> mapSolarSystems, List<string> nameDbs)
        {
            Listener = listener;
            _chatChanelInfos = chatChanelInfos;
            _mapSolarSystems = mapSolarSystems;
            _nameDbs = nameDbs;
            Setting = Services.Settings.IntelSettingService.GetValue(listener);
            if(Setting == null)
            {
                Setting = new Core.Models.ChannelIntel.ChannelIntelSetting();
                Setting.Listener = listener;
            }
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
                SelectedMapSolarSystem = _mapSolarSystems.FirstOrDefault(p => p.SolarSystemID == Setting.LocationID);
            }
            else
            {
                SelectedMapSolarSystem = null;
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
        }

        private void SaveSetting()
        {
            Setting.ChannelIDs = ChatChanelInfos.Where(p => p.IsChecked).Select(p => p.ChannelID).ToList();
            Setting.LocationID = SelectedMapSolarSystem == null ? -1 : SelectedMapSolarSystem.SolarSystemID;
            Setting.NameDbs = SelectedNameDbs.ToList();
            Services.Settings.IntelSettingService.SetValue(Setting);
        }
        private void Setting_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Core.Models.EarlyWarningSetting.AutoUpdateLocaltion))
            {
                //如果勾选上自动更新位置，立即获取当前位置
                if (Setting.AutoUpdateLocaltion)
                {
                    UpdateCharacterLocation();
                }
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
                        SelectedMapSolarSystem = _mapSolarSystems.FirstOrDefault(p => p.SolarSystemID == systemId);
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
            _chatChanelInfos = chatChanelInfos;
        }

        public async Task Start()
        {
            if (string.IsNullOrEmpty(LocationSolarSystem))
            {
                throw new Exception("请设置角色当前所处星系");
            }
            var location = _mapSolarSystems.FirstOrDefault(p => p.SolarSystemName == LocationSolarSystem);
            if (location == null)
            {
                throw new Exception("无效的星系名称");
            }
            if (ChatChanelInfos.NotNullOrEmpty())
            {
                _observers.Clear();
                SelectedMapSolarSystem = location;
                Setting.LocationID = location.SolarSystemID;
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
            throw new NotImplementedException();
        }

        private void Observer_OnContentUpdate(Core.Models.ChannelIntel.ChannelIntelObserver channelIntelObserver, IEnumerable<Core.Models.EVELogs.IntelChatContent> news)
        {
            throw new NotImplementedException();
        }
        private void Observer_LocalChanged(Core.Models.ChannelIntel.ChannelIntelObserver channelIntelObserver, IEnumerable<Core.Models.EVELogs.IntelChatContent> news)
        {
            throw new NotImplementedException();
        }
        public event EventHandler<Core.Models.EarlyWarningContent> ZKBIntelEvent;
        private void ZkbIntel_OnWarningUpdate(object sender, Core.Models.EarlyWarningContent e)
        {
            ZKBIntelEvent?.Invoke(this, e);
        }
        private void IntelWindow_OnStop()
        {
            Stop();
        }
        public void Stop()
        {

        }
    }
}
