using CommunityToolkit.Mvvm.Input;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.WinUI.Models;
using TheGuideToTheNewEden.WinUI.Services;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class ChannelMonitorViewModel : BaseViewModel
    {
        /// <summary>
        /// key为角色名
        /// value为角色下所有的不重复频道
        /// 每个频道都是该频道最新日期的那个文件
        /// </summary>
        private readonly Dictionary<string, List<ChatChanelInfo>> _listenerChannelDic = new Dictionary<string, List<ChatChanelInfo>>();

        private Core.Models.ChannelMonitorItem _selectedCharacter;
        public Core.Models.ChannelMonitorItem SelectedCharacter
        {
            get => _selectedCharacter;
            set
            {
                if (SetProperty(ref _selectedCharacter, value))
                {
                    UpdateSelectedCharacter();
                }
            }
        }

        private List<Core.Models.ChannelMonitorItem> _characters;
        public List<Core.Models.ChannelMonitorItem> Characters
        {
            get => _characters;
            set => SetProperty(ref _characters, value);
        }

        private List<ChatChanelInfo> _chatChanelInfos;
        /// <summary>
        /// 当前角色所有聊天频道
        /// </summary>
        public List<ChatChanelInfo> ChatChanelInfos
        {
            get => _chatChanelInfos;
            set => SetProperty(ref _chatChanelInfos, value);
        }


        private bool _running;
        public bool Running
        {
            get => _running;
            set => SetProperty(ref _running, value);
        }

        private Dictionary<string, Core.Models.ChatlogObservableItem[]> _runningChatlogObservableItems = new Dictionary<string, Core.Models.ChatlogObservableItem[]>();
        public ChannelMonitorViewModel()
        {
            InitDicAsync();
        }
        internal delegate void SelectedCharacterChanged(string selectedCharacter);
        internal event SelectedCharacterChanged OnSelectedCharacterChanged;
        private void UpdateSelectedCharacter()
        {
            if(_selectedCharacter != null)
            {
                if (_listenerChannelDic.TryGetValue(_selectedCharacter.Name, out var chatChanelInfos))
                {
                    ChatChanelInfos = chatChanelInfos;
                }
                OnSelectedCharacterChanged?.Invoke(_selectedCharacter.Name);
                if (_selectedCharacter.Setting == null)
                {
                    var setting = Services.Settings.ChannelMonitorSettingService.GetValue(_selectedCharacter.Name);
                    setting ??= new Core.Models.ChannelMonitorSetting();
                    setting.Name = _selectedCharacter.Name;
                    _selectedCharacter.Setting = setting;
                }
                if(!ChatChanelInfos.IsNullOrEmpty() && !_selectedCharacter.Setting.SelectedChannels.IsNullOrEmpty())
                {
                    foreach (var channel in ChatChanelInfos)
                    {
                        var target = _selectedCharacter.Setting.SelectedChannels.FirstOrDefault(p => p == channel.ChannelName);
                        if(target != null)
                        {
                            channel.IsChecked = true;
                        }
                    }
                }
            }
        }
        private async void InitDicAsync()
        {
            _listenerChannelDic.Clear();
            string path = Services.Settings.GameLogsSettingService.GetChatlogsPath();
            if (System.IO.Directory.Exists(path))
            {
                ShowWaiting();
                try
                {
                    await Task.Run(() =>
                    {
                        var dic = GameLogHelper.GetChatChanelInfos(path, Services.Settings.GameLogsSettingService.EVELogsChannelDurationValue);
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
                catch(Exception ex)
                {
                    Core.Log.Error(ex);
                }
                HideWaiting();
            }
            if (_listenerChannelDic.Any())
            {
                Characters = _listenerChannelDic.Select(p => new Core.Models.ChannelMonitorItem() { Name = p.Key}).ToList();
            }
            else
            {
                Characters = null;
            }
            ChatChanelInfos = null;
        }

        public ICommand RefreshListCommand => new RelayCommand(() =>
        {
            InitDicAsync();
        });
        public ICommand RefreshChannelListCommand => new RelayCommand(() =>
        {
            InitDicAsync();
        });
        public ICommand PickSoundFileCommand => new RelayCommand(async () =>
        {
            try
            {
                var file = await Helpers.PickHelper.PickFileAsync(Window);
                if (file != null)
                {
                    SelectedCharacter.Setting.SoundFile = file.Path;
                }
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                ShowError(ex.Message);
            }
        });
        public ICommand AddKeysCommand => new RelayCommand(() =>
        {
            SelectedCharacter.Setting.Keys.Add(new Core.Models.ChannelMonitorKey(".*"));
        });
        public ICommand StopNotifyCommand => new RelayCommand(() =>
        {
            ChannelMonitorNotifyService.Current.Stop(SelectedCharacter.Name);
        });
        public ICommand StopCommand => new RelayCommand(() =>
        {
            Stop(SelectedCharacter);
            Running = Characters.FirstOrDefault(p => p.Running) != null;
        });
        public ICommand StopAllCommand => new RelayCommand(() =>
        {
            foreach (var character in Characters)
            {
                if (character.Running)
                {
                    Stop(character);
                }
            }
            Running = Characters.FirstOrDefault(p => p.Running) != null;
        });
        public ICommand StartCommand => new RelayCommand(() =>
        {
            var channelInfos = _chatChanelInfos;
            if (channelInfos == null && _selectedCharacter != null)
            {
                _listenerChannelDic.TryGetValue(_selectedCharacter.Name, out channelInfos);
            }
            SelectedCharacter.Setting.SelectedChannels = (channelInfos ?? Enumerable.Empty<ChatChanelInfo>())
                .Where(p => p.IsChecked).Select(p => p.ChannelName).ToList();
            if (Start(SelectedCharacter))
            {
                Running = true;
            }
        });
        public ICommand StartAllCommand => new RelayCommand(() =>
        {
            var channelInfos = _chatChanelInfos;
            if (channelInfos == null && _selectedCharacter != null)
            {
                _listenerChannelDic.TryGetValue(_selectedCharacter.Name, out channelInfos);
            }
            foreach (var character in Characters)
            {
                if(character == _selectedCharacter)
                {
                    character.Setting.SelectedChannels = (channelInfos ?? Enumerable.Empty<ChatChanelInfo>())
                        .Where(p => p.IsChecked).Select(p => p.ChannelName).ToList();
                }
                if (!character.Running)
                {
                    Start(character);
                }
            }
            Running = Characters.FirstOrDefault(p => p.Running) != null;
        });
        private bool Start(Core.Models.ChannelMonitorItem channelMonitorItem)
        {
            if (channelMonitorItem.Setting == null)
            {
                var setting = Services.Settings.ChannelMonitorSettingService.GetValue(channelMonitorItem.Name);
                if (setting != null)
                {
                    channelMonitorItem.Setting = setting;
                }
                else
                { 
                    return false;
                }
            }
            if (!channelMonitorItem.Setting.Keys.Any())
            {
                ShowError($"{channelMonitorItem.Name}: {Helpers.ResourcesHelper.GetString("GameLogMonitorPage_NoneKeyError")}");
                return false;
            }
            _listenerChannelDic.TryGetValue(channelMonitorItem.Name, out var chatChanelInfos);
            if (!chatChanelInfos.IsNullOrEmpty())
            {
                if (!channelMonitorItem.Setting.SelectedChannels.IsNullOrEmpty())
                {
                    foreach (var channel in chatChanelInfos)
                    {
                        var target = channelMonitorItem.Setting.SelectedChannels.FirstOrDefault(p => p == channel.ChannelName);
                        if (target != null)
                        {
                            channel.IsChecked = true;
                        }
                    }
                }
                else
                {
                    ShowError($"{channelMonitorItem.Name}: {Helpers.ResourcesHelper.GetString("ChannelMonitorPage_NoneSelectedChannel")}");
                    return false;
                }
            }
            if (!(chatChanelInfos?.FirstOrDefault(p => p.IsChecked) != null))
            {
                ShowError($"{channelMonitorItem.Name}: {Helpers.ResourcesHelper.GetString("ChannelMonitorPage_NoneSelectedChannel")}");
                return false;
            }
            try
            {
                if (ChannelMonitorNotifyService.Current.Add(channelMonitorItem))
                {
                    _runningChatlogObservableItems.Remove(channelMonitorItem.Name);
                    List<Core.Models.ChatlogObservableItem> items = new List<Core.Models.ChatlogObservableItem>();
                    var selectedC = chatChanelInfos.Where(p => p.IsChecked).ToList();
                    foreach (var item in selectedC)
                    {
                        Core.Models.ChatlogObservableItem chatlogObservableItem = new Core.Models.ChatlogObservableItem(item, channelMonitorItem);
                        items.Add(chatlogObservableItem);
                        Core.Services.ObservableFileService.Add(chatlogObservableItem);
                        chatlogObservableItem.OnContentUpdate += ChatlogObservableItem_OnContentUpdate;
                    }
                    _runningChatlogObservableItems.Add(channelMonitorItem.Name, items.ToArray());
                    Services.Settings.ChannelMonitorSettingService.SetValue(channelMonitorItem.Setting);
                    channelMonitorItem.Running = true;
                    return true;
                }
                else
                {
                    ShowError($"{channelMonitorItem.Name}: {Helpers.ResourcesHelper.GetString("GameLogMonitorPage_AddNotifyServiceFalied")}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                ChannelMonitorNotifyService.Current.Remove(channelMonitorItem.Name);
                _runningChatlogObservableItems.Remove(channelMonitorItem.Name);
                Core.Log.Error(ex);
                ShowError(ex.Message);
                return false;
            }
        }
        private void Stop(Core.Models.ChannelMonitorItem channelMonitorItem)
        {
            ChannelMonitorNotifyService.Current.Stop(channelMonitorItem.Name);
            ChannelMonitorNotifyService.Current.Remove(channelMonitorItem.Name);
            if (_runningChatlogObservableItems.TryGetValue(channelMonitorItem.Name, out var items))
            {
                foreach (var item in items)
                {
                    item.OnContentUpdate -= ChatlogObservableItem_OnContentUpdate;
                    Core.Services.ObservableFileService.Remove(item);
                }
            }
            _runningChatlogObservableItems.Remove(channelMonitorItem.Name);
            channelMonitorItem.Running = false;
        }

        public delegate void ContentUpdate(string name, IEnumerable<Core.Models.EVELogs.ChatContent> chatContents);
        /// <summary>
        /// 消息更新
        /// </summary>
        public event ContentUpdate OnContentUpdate;
        private void ChatlogObservableItem_OnContentUpdate(Core.Models.ChatlogObservableItem item, IEnumerable<Core.Models.EVELogs.ChatContent> news)
        {
            OnContentUpdate?.Invoke(item.ChannelMonitorItem.Name,news);
            foreach (var msg in news)
            {
                if (msg.Important)
                {
                    ChannelMonitorNotifyService.Current.Notify(item.ChannelMonitorItem, msg.SourceContent);
                }
            }
        }
    }
}
