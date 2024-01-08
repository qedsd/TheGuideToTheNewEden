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
        private HashSet<string> _runningCharacters;
        /// <summary>
        /// key为角色名
        /// value为角色下所有的不重复频道
        /// 每个频道都是该频道最新日期的那个文件
        /// </summary>
        private readonly Dictionary<string, List<ChatChanelInfo>> _listenerChannelDic = new Dictionary<string, List<ChatChanelInfo>>();

        private Core.Models.ChannelMonitorItem selectedCharacter;
        public Core.Models.ChannelMonitorItem SelectedCharacter
        {
            get => selectedCharacter;
            set
            {
                if (SetProperty(ref selectedCharacter, value))
                {
                    UpdateSelectedCharacter();
                }
            }
        }

        private List<Core.Models.ChannelMonitorItem> characters;
        public List<Core.Models.ChannelMonitorItem> Characters
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

        private Dictionary<string, Core.Models.ChatlogObservableItem[]> _runningChatlogObservableItems = new Dictionary<string, Core.Models.ChatlogObservableItem[]>();
        public ChannelMonitorViewModel()
        {
            InitDicAsync();
        }
        internal delegate void SelectedCharacterChanged(string selectedCharacter);
        internal event SelectedCharacterChanged OnSelectedCharacterChanged;
        private void UpdateSelectedCharacter()
        {
            if(selectedCharacter != null)
            {
                if (_listenerChannelDic.TryGetValue(selectedCharacter.Name, out var chatChanelInfos))
                {
                    ChatChanelInfos = chatChanelInfos;
                }
                OnSelectedCharacterChanged?.Invoke(selectedCharacter.Name);
                if (selectedCharacter.Setting == null)
                {
                    var setting = Services.Settings.ChannelMonitorSettingService.GetValue(selectedCharacter.Name);
                    setting ??= new Core.Models.ChannelMonitorSetting();
                    setting.Name = selectedCharacter.Name;
                    selectedCharacter.Setting = setting;
                }
                if(!ChatChanelInfos.IsNullOrEmpty() && !selectedCharacter.Setting.SelectedChannels.IsNullOrEmpty())
                {
                    foreach (var channel in ChatChanelInfos)
                    {
                        var target = selectedCharacter.Setting.SelectedChannels.FirstOrDefault(p => p == channel.ChannelName);
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
                Window?.ShowWaiting();
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
                Window?.HideWaiting();
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
                Window?.ShowError(ex.Message);
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
            ChannelMonitorNotifyService.Current.Stop(SelectedCharacter.Name);
            ChannelMonitorNotifyService.Current.Remove(SelectedCharacter.Name);
            if(_runningChatlogObservableItems.TryGetValue(SelectedCharacter.Name, out var items))
            {
                foreach(var item in items)
                {
                    Core.Services.ObservableFileService.Remove(item.FilePath);
                }
            }
            _runningChatlogObservableItems.Remove(SelectedCharacter.Name);
            SelectedCharacter.Running = false;
        });
        public ICommand StartCommand => new RelayCommand(() =>
        {
            if(!SelectedCharacter.Setting.Keys.Any())
            {
                ShowError(Helpers.ResourcesHelper.GetString("GameLogMonitorPage_NoneKeyError"));
                return;
            }
            if (!(ChatChanelInfos?.FirstOrDefault(p=>p.IsChecked) != null))
            {
                ShowError(Helpers.ResourcesHelper.GetString("ChannelMonitorPage_NoneSelectedChannel"));
                return;
            }
            try
            {
                if (ChannelMonitorNotifyService.Current.Add(SelectedCharacter))
                {
                    _runningChatlogObservableItems.Remove(SelectedCharacter.Name);
                    List<Core.Models.ChatlogObservableItem> items = new List<Core.Models.ChatlogObservableItem>();
                    var selectedC = ChatChanelInfos.Where(p => p.IsChecked).ToList();
                    foreach (var item in selectedC)
                    {
                        Core.Models.ChatlogObservableItem chatlogObservableItem = new Core.Models.ChatlogObservableItem(item, SelectedCharacter);
                        items.Add(chatlogObservableItem);
                        Core.Services.ObservableFileService.Add(chatlogObservableItem);
                        chatlogObservableItem.OnContentUpdate += ChatlogObservableItem_OnContentUpdate;
                    }
                    _runningChatlogObservableItems.Add(SelectedCharacter.Name, items.ToArray());
                    SelectedCharacter.Setting.SelectedChannels = selectedC.Select(p => p.ChannelName).ToList();
                    Services.Settings.ChannelMonitorSettingService.SetValue(SelectedCharacter.Setting);
                    SelectedCharacter.Running = true;
                }
            }
            catch(Exception ex)
            {
                ChannelMonitorNotifyService.Current.Remove(SelectedCharacter.Name);
                _runningChatlogObservableItems.Remove(SelectedCharacter.Name);
                Core.Log.Error(ex);
                Window?.ShowError(ex.Message);
            }

        });
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
