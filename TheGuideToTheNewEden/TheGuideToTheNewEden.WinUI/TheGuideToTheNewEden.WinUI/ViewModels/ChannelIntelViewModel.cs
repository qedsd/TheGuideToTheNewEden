using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Intel;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WinUI.Models;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Services.Settings;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class ChannelIntelViewModel : BaseViewModel
    {
        private string _logPath;
        /// <summary>
        /// key为角色名
        /// value为角色下所有的不重复频道
        /// 每个频道都是该频道最新日期的那个文件
        /// </summary>
        private Dictionary<string, List<ChatChanelInfo>> _listenerChannelDic = new Dictionary<string, List<ChatChanelInfo>>();
        private Dictionary<string, ChannelIntel> _channelIntels = new Dictionary<string, ChannelIntel>();

        private List<string> _nameDbs;
        public List<string> NameDbs
        {
            get => _nameDbs;
            set => SetProperty(ref _nameDbs, value);
        }

        private List<Core.DBModels.MapSolarSystemBase> _mapSolarSystems;
        public List<Core.DBModels.MapSolarSystemBase> MapSolarSystems
        {
            get => _mapSolarSystems;
            set => SetProperty(ref _mapSolarSystems, value);
        }

        private ChannelIntelListener _selectedCharacter;
        public ChannelIntelListener SelectedCharacter
        {
            get => _selectedCharacter;
            set
            {
                if (SetProperty(ref _selectedCharacter, value))
                {
                    UpdateSelectedCharacter(value.Name);
                }
            }
        }

        private List<ChannelIntelListener> _characters;
        public List<ChannelIntelListener> Characters
        {
            get => _characters;
            set => SetProperty(ref _characters, value);
        }

        private ChannelIntel _channelIntel;
        public ChannelIntel ChannelIntel
        {
            get => _channelIntel;
            set
            {
                SetProperty(ref _channelIntel, value);
            }
        }

        public ChannelIntelViewModel()
        {
            _logPath = System.IO.Path.Combine(GameLogsSettingService.EVELogsPathValue, "Chatlogs");
            InitNameDbs();
            InitSolarSystems();
            InitDicAsync();
        }
        private void InitNameDbs()
        {
            NameDbs = new List<string>()
            {
                "default(en)"
            };
            var local = LocalDbSelectorService.GetAll();
            if (local.NotNullOrEmpty())
            {
                NameDbs.AddRange(local);
            }
        }
        private async void InitDicAsync()
        {
            _listenerChannelDic.Clear();
            if (System.IO.Directory.Exists(_logPath))
            {
                await Task.Run(() => 
                {
                    var dic = GameLogHelper.GetChatChanelInfos(_logPath, Services.Settings.GameLogsSettingService.EVELogsChannelDurationValue);
                    if (dic != null)
                    {
                        foreach (var item in dic)
                        {
                            List<ChatChanelInfo> chatChanelInfos = new List<ChatChanelInfo>();
                            foreach (var coreChatChanelInfos in item.Value)
                            {
                                chatChanelInfos.Add(ChatChanelInfo.Create(coreChatChanelInfos));
                            }
                            _listenerChannelDic.Add(item.Key, chatChanelInfos);
                        }
                    }
                });
            }
            if (_listenerChannelDic.Count != 0)
            {
                Characters = _listenerChannelDic.Select(p => new ChannelIntelListener(p.Key)).ToList();
            }
            else
            {
                Characters = null;
            }
        }
        private async void InitSolarSystems()
        {
            var list = await Core.Services.DB.MapSolarSystemService.QueryAllAsync();
            if (list.NotNullOrEmpty())
            {
                MapSolarSystems = list.Select(p => p as Core.DBModels.MapSolarSystemBase).ToList();
            }
        }

        private void UpdateSelectedCharacter(string name)
        {
            ChannelIntel = GetChannelIntel(name);
        }
        private ChannelIntel GetChannelIntel(string name)
        {
            if (_channelIntels.TryGetValue(name, out var channelIntel))
            {
                return channelIntel;
            }
            else
            {
                return new ChannelIntel(name, _listenerChannelDic[name], _mapSolarSystems, _nameDbs);
            }
        }

        public ICommand StartCommand => new RelayCommand(async () =>
        {
            if (SelectedCharacter == null)
            {
                Window.ShowError("请选择角色");
                return;
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
            _zkbIntel?.Stop();
            Services.WarningService.Current.Remove(Setting?.Listener);
            GC.Collect();
        });

        public ICommand RestorePosCommand => new RelayCommand(() =>
        {
            if (!string.IsNullOrEmpty(Setting?.Listener))
            {
                if (WarningService.Current.RestoreWindowPos(Setting.Listener))
                {
                    Window?.ShowSuccess("重置成功");
                }
                else
                {
                    Window?.ShowError("重置失败");
                }
            }
        });

        public ICommand StopSoundCommand => new RelayCommand(() =>
        {
            if (!string.IsNullOrEmpty(Setting?.Listener))
            {
                WarningService.Current.StopSound(Setting.Listener);
            }
        });

        /// <summary>
        /// 预警更新
        /// </summary>
        /// <param name="earlyWarningItem"></param>
        /// <param name="news"></param>
        private void EarlyWarningItem_OnWarningUpdate(Core.Models.EarlyWarningItem earlyWarningItem, IEnumerable<Core.Models.EarlyWarningContent> news)
        {
            Window.DispatcherQueue.TryEnqueue(() =>
            {
                foreach (var ch in news)
                {
                    if (ch.IntelType == Core.Enums.IntelChatType.Intel)
                    {
                        Core.Models.WarningSoundSetting soundSetting = null;
                        if (Setting.Sounds.Count >= ch.Jumps)
                        {
                            soundSetting = Setting.Sounds[ch.Jumps];
                        }
                        WarningService.Current.Notify(earlyWarningItem.ChatChanelInfo.Listener, soundSetting, Setting.SystemNotify, earlyWarningItem.ChatChanelInfo.ChannelName, ch);
                    }
                    else
                    {
                        //只clr
                        WarningService.Current.GetIntelWindow(earlyWarningItem.ChatChanelInfo.Listener)?.Intel(ch);
                    }
                }
            });
        }
        /// <summary>
        /// 频道内容更新
        /// </summary>
        /// <param name="earlyWarningItem"></param>
        /// <param name="newlines"></param>
        private void EarlyWarningItem_OnContentUpdate(Core.Models.EarlyWarningItem earlyWarningItem, IEnumerable<IntelChatContent> news)
        {
            Window.DispatcherQueue.TryEnqueue(() =>
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
            for (int i = news.Count() - 1; i >= 0; i--)//从后面往回找，新消息位于后面
            {
                var id = await Core.EVEHelpers.ChatLogHelper.TryGetCharacterLocationAsync(news.ElementAt(i), NameDbs);
                if (id > 0)
                {
                    Window.DispatcherQueue.TryEnqueue(async () =>
                    {
                        Setting.LocationID = id;
                        SelectedMapSolarSystem = MapSolarSystems.FirstOrDefault(p => p.SolarSystemID == id);
                        IntelMap = await Core.EVEHelpers.SolarSystemPosHelper.GetIntelSolarSystemMapAsync(Setting.LocationID, Setting.IntelJumps);
                        Core.EVEHelpers.SolarSystemPosHelper.ResetXY(IntelMap.GetAllSolarSystem());
                        foreach (var item in EarlyWarningItems)
                        {
                            item.IntelMap = IntelMap;
                        }
                        WarningService.Current.UpdateWindowHome(Setting.Listener, IntelMap);
                    });
                    break;
                }
            }
        }
        private void ZkbIntel_OnWarningUpdate(object sender, Core.Models.EarlyWarningContent e)
        {
            Window.DispatcherQueue.TryEnqueue(() =>
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
                Core.Models.WarningSoundSetting soundSetting = null;
                if (Setting.Sounds.Count >= e.Jumps)
                {
                    soundSetting = Setting.Sounds[e.Jumps];
                }
                ZKBIntelContents.Add(e);
                WarningService.Current.Notify((sender as Core.Intel.ZKBIntel).GetListener(), soundSetting, Setting.SystemNotify, "KB", e);
            });
        }
    }
}
