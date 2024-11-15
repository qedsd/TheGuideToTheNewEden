﻿using CommunityToolkit.Mvvm.Input;
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
        private readonly string _logPath;
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
                    UpdateSelectedCharacter(value?.Name);
                }
            }
        }

        private ObservableCollection<ChannelIntelListener> _characters;
        public ObservableCollection<ChannelIntelListener> Characters
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

        private bool _running;
        public bool Running
        {
            get => _running;
            set => SetProperty(ref _running, value);
        }

        public ObservableCollection<IntelChatContent> ChatContents { get; set; } = new ObservableCollection<IntelChatContent>();
        public ObservableCollection<Core.Models.EarlyWarningContent> ZKBIntelContents { get; set; } = new ObservableCollection<Core.Models.EarlyWarningContent>();

        private List<Core.DBModels.MapSolarSystemBase> searchMapSolarSystems;
        public List<Core.DBModels.MapSolarSystemBase> SearchMapSolarSystems
        {
            get => searchMapSolarSystems;
            set => SetProperty(ref searchMapSolarSystems, value);
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
            await LoadListenerChannelDic();
            if (_listenerChannelDic.Count != 0)
            {
                Characters = _listenerChannelDic.Select(p => new ChannelIntelListener(p.Key)).ToObservableCollection();
            }
            else
            {
                Characters = new ObservableCollection<ChannelIntelListener>();
            }
        }
        private async Task LoadListenerChannelDic()
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
            ChannelIntel = string.IsNullOrEmpty(name) ? null : GetChannelIntel(name);
        }
        private ChannelIntel GetChannelIntel(string name)
        {
            if (_channelIntels.TryGetValue(name, out var channelIntel))
            {
                return channelIntel;
            }
            else
            {
                ChannelIntel c =  new ChannelIntel(name, _listenerChannelDic[name], _mapSolarSystems, _nameDbs, Window.DispatcherQueue);
                _channelIntels.Add(name, c);
                return c;
            }
        }
        private async Task<bool> Start(ChannelIntel channelIntel, ChannelIntelListener channelIntelListener)
        {
            if (channelIntel == null)
            {
                Window.ShowError("请选择角色");
                return false;
            }
            try
            {
                await channelIntel.Start();
                channelIntelListener.Running = true;
                channelIntel.ChatContentEvent -= ChannelIntel_ChatContentEvent;
                channelIntel.ChatContentEvent += ChannelIntel_ChatContentEvent;
                channelIntel.ZKBIntelEvent -= ChannelIntel_ZKBIntelEvent;
                channelIntel.ZKBIntelEvent += ChannelIntel_ZKBIntelEvent;
                return true;
            }
            catch (Exception ex)
            {
                Services.WarningService.Current.Remove(channelIntel.Listener);
                Core.Log.Error(ex);
                Window.ShowError(ex.Message);
                return false;
            }
        }
        public ICommand StartCommand => new RelayCommand(async() =>
        {
            ShowWaiting();
            if(await Start(ChannelIntel, SelectedCharacter))
            {
                Running = true;
            }
            HideWaiting();
        });
        public ICommand StartAllCommand => new RelayCommand(async () =>
        {
            foreach(var c in Characters)
            {
                await Start(GetChannelIntel(c.Name), c);
            }
            Running = true;
        });
        public ICommand StopCommand => new RelayCommand(() =>
        {
            ChannelIntel.Stop();
            SelectedCharacter.Running = false;
            if(Characters.Count(p => p.Running) == 0)
            {
                Running = false;
            }
        });
        public ICommand StopAllCommand => new RelayCommand(() =>
        {
            foreach(var c in _channelIntels.Values)
            {
                c.Stop();
            }
            foreach(var c in Characters)
            {
                c.Running = false;
            }
            Running = false;
        });

        public ICommand RestorePosCommand => new RelayCommand(() =>
        {
            if (ChannelIntel.RestorePos())
            {
                Window?.ShowSuccess("重置成功");
            }
            else
            {
                Window?.ShowError("重置失败");
            }
        });

        public ICommand StopSoundCommand => new RelayCommand(() =>
        {
            ChannelIntel?.StopSound();
        });
        public ICommand RefreshChannelsCommand => new RelayCommand(async () =>
        {
            string name = ChannelIntel.Listener;
            ShowWaiting();
            var channels = await Task.Run(() =>
            {
                var dic = GameLogHelper.GetChatChanelInfos(_logPath, Services.Settings.GameLogsSettingService.EVELogsChannelDurationValue);
                if(dic.TryGetValue(name, out var list))
                {
                    List<ChatChanelInfo> chatChanelInfos = new List<ChatChanelInfo>();
                    foreach (var coreChatChanelInfos in list)
                    {
                        chatChanelInfos.Add(ChatChanelInfo.Create(coreChatChanelInfos));
                    }
                    return chatChanelInfos;
                }
                else
                {
                    return null;
                }
            });
            ChannelIntel.UpdateChannels(channels);
            HideWaiting();
        });
        public ICommand RefreshCharactersCommand => new RelayCommand(async () =>
        {
            ShowWaiting();
            await LoadListenerChannelDic();
            if (_listenerChannelDic.Any())
            {
                foreach(var item in _listenerChannelDic)
                {
                    if(Characters.FirstOrDefault(p=>p.Name == item.Key) == null)
                    {
                        Characters.Add(new ChannelIntelListener(item.Key));
                    }
                }
            }
            SelectedCharacter = null;
            HideWaiting();
        });
        public ICommand ApplySettingToAllCommand => new RelayCommand(async () =>
        {
            Microsoft.UI.Xaml.Controls.ContentDialog contentDialog = new Microsoft.UI.Xaml.Controls.ContentDialog()
            {
                XamlRoot = Window.Content.XamlRoot,
                Title = Helpers.ResourcesHelper.GetString("ChannelIntelPage_ApplySettingToAll"),
                Content = new Microsoft.UI.Xaml.Controls.TextBlock()
                {
                    Text = Helpers.ResourcesHelper.GetString("ChannelIntelPage_ApplySettingToAll_Tip")
                },
                PrimaryButtonText = Helpers.ResourcesHelper.GetString("General_OK"),
                CloseButtonText = Helpers.ResourcesHelper.GetString("General_Cancel"),
            };
            if (await contentDialog.ShowAsync() == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                StopAllCommand.Execute(null);
                foreach (var item in Characters)
                {
                    var intelItem = GetChannelIntel(item.Name);
                    if(intelItem != null && intelItem != ChannelIntel)
                    {
                        intelItem.Setting.AutoUpdateLocaltion = ChannelIntel.Setting.AutoUpdateLocaltion;
                        intelItem.Setting.IntelJumps = ChannelIntel.Setting.IntelJumps;
                        intelItem.Setting.OverlapType = ChannelIntel.Setting.OverlapType;
                        intelItem.Setting.OverlapNotify = ChannelIntel.Setting.OverlapNotify;
                        intelItem.Setting.OverlapStyle = ChannelIntel.Setting.OverlapStyle;
                        intelItem.Setting.MakeSound = ChannelIntel.Setting.MakeSound;
                        intelItem.Setting.Sounds.Clear();
                        foreach (var sound in ChannelIntel.Setting.Sounds)
                        {
                            intelItem.Setting.Sounds.Add(sound.DepthClone<ChannelIntelSoundSetting>());
                        }
                        intelItem.Setting.SystemNotify = ChannelIntel.Setting.SystemNotify;
                        intelItem.Setting.NameDbs = ChannelIntel.Setting.NameDbs;
                        intelItem.Setting.IgnoreWords = ChannelIntel.Setting.IgnoreWords;
                        intelItem.Setting.ClearWords = ChannelIntel.Setting.ClearWords;
                        intelItem.Setting.AutoClear = ChannelIntel.Setting.AutoClear;
                        intelItem.Setting.AutoClearMinute = ChannelIntel.Setting.AutoClearMinute;
                        intelItem.Setting.AutoDowngrade = ChannelIntel.Setting.AutoDowngrade;
                        intelItem.Setting.AutoDowngradeMinute = ChannelIntel.Setting.AutoDowngradeMinute;
                        intelItem.Setting.OverlapOpacity = ChannelIntel.Setting.OverlapOpacity;
                        intelItem.Setting.SubZKB = ChannelIntel.Setting.SubZKB;
                        intelItem.Setting.KBTime = ChannelIntel.Setting.KBTime;
                        Services.Settings.IntelSettingService.SetValue(intelItem.Setting);
                    }
                }
                Window?.ShowSuccess(Helpers.ResourcesHelper.GetString("ChannelIntelPage_ApplySettingToAll_Succes"));
            }
        });
        private void ChannelIntel_ZKBIntelEvent(object sender, Core.Models.EarlyWarningContent e)
        {
            Window.DispatcherQueue.TryEnqueue(() =>
            {
                ZKBIntelContents.Add(e);
            });
        }

        private void ChannelIntel_ChatContentEvent(object sender, IEnumerable<IntelChatContent> e)
        {
            Window.DispatcherQueue.TryEnqueue(() =>
            {
                foreach (IntelChatContent content in e)
                {
                    ChatContents.Add(content);
                }
            });
        }
    }
}
