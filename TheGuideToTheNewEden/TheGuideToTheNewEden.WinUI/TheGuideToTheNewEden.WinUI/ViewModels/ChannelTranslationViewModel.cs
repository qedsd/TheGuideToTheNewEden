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
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Intel;
using TheGuideToTheNewEden.Core.Models.Channel;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Models;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Services.Settings;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class ChannelTranslationViewModel : BaseViewModel, IDisposable
    {
        private readonly string _logPath;
        /// <summary>
        /// key为角色名
        /// value为角色下所有的不重复频道
        /// 每个频道都是该频道最新日期的那个文件
        /// </summary>
        private readonly Dictionary<string, List<ChatChanelInfo>> _listenerChannelDic = new Dictionary<string, List<ChatChanelInfo>>();
        private readonly Dictionary<string, ChannelTranslation> _channelTranslations = new Dictionary<string, ChannelTranslation>();

        public List<string> Froms { get; set; } = new List<string>() { "auto", "de", "en", "es", "fr", "it", "ja", "ko", "ru", "zh-CHS", "zh-CHT" };
        public List<string> Tos { get; set; } = new List<string>() { "zh-CHS", "zh-CHT", "de", "en", "es", "fr", "it", "ja", "ko", "ru", };

        private ChannelListener _selectedCharacter;
        public ChannelListener SelectedCharacter
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

        private ObservableCollection<ChannelListener> _characters;
        public ObservableCollection<ChannelListener> Characters
        {
            get => _characters;
            set => SetProperty(ref _characters, value);
        }

        private ChannelTranslation _channelTranslation;
        public ChannelTranslation ChannelTranslation
        {
            get => _channelTranslation;
            set
            {
                SetProperty(ref _channelTranslation, value);
            }
        }

        private List<ChatChanelInfo> _chatChanelInfos;
        internal List<ChatChanelInfo> ChatChanelInfos
        {
            get => _chatChanelInfos;
            set
            {
                SetProperty(ref _chatChanelInfos, value);
            }
        }

        private bool _running;
        public bool Running
        {
            get => _running;
            set => SetProperty(ref _running, value);
        }
        public ChannelTranslationViewModel()
        {
            _logPath = System.IO.Path.Combine(GameLogsSettingService.EVELogsPathValue, "Chatlogs");
            ClientServiceHelper.GetRequiredService<ChannelTranslationService>().Start();
            Init();
        }
        private void Init()
        {
            InitDicAsync();
        }
        private async void InitDicAsync()
        {
            await LoadListenerChannelDic();
            if (_listenerChannelDic.Count != 0)
            {
                Characters = _listenerChannelDic.Select(p => new ChannelListener(p.Key)).ToObservableCollection();
            }
            else
            {
                Characters = new ObservableCollection<ChannelListener>();
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
        private void UpdateSelectedCharacter(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                ChannelTranslation = null;
                ChatChanelInfos = null;
            }
            else
            {
                ChannelTranslation = GetChannelTranslation(name);
                ChatChanelInfos = _listenerChannelDic[name];
                foreach (var channel in ChatChanelInfos)
                {
                    channel.IsChecked = ChannelTranslation.Setting.Channels.Contains(channel.FilePath);
                }
            }
        }
        private ChannelTranslation GetChannelTranslation(string name)
        {
            if (!_channelTranslations.TryGetValue(name, out var channel))
            {
                channel = new ChannelTranslation(name);
                _channelTranslations.Add(name, channel);
            }
            return channel;
        }

        private bool Start(ChannelTranslation channel)
        {
            if (channel != null && channel.Setting.Channels.NotNullOrEmpty())
            {
                try
                {
                    channel.Start();
                    return true;
                }
                catch (Exception ex)
                {
                    Core.Log.Error(ex);
                    ShowError(ex.Message);
                    return false;
                }
            }
            else
            {
                ShowError(Helpers.ResourcesHelper.GetString("Channel_NoChannel"));
            }
            return false;
        }

        private void Stop(ChannelTranslation channel)
        {
            channel?.Stop();
        }
        private void StopAll()
        {
            foreach (var character in Characters)
            {
                if (character.Running)
                {
                    if (_channelTranslations.TryGetValue(character.Name, out var channel))
                    {
                        channel.Stop();
                    }
                    character.Running = false;
                }
            }
        }
        public ICommand StartCommand => new RelayCommand(() =>
        {
            if (ChannelTranslation != null && ChatChanelInfos.NotNullOrEmpty())
            {
                var selectedChannels = ChatChanelInfos.Where(p => p.IsChecked).ToList();
                if (selectedChannels.NotNullOrEmpty())
                {
                    ChannelTranslation.SetSelectedChannels(selectedChannels.Select(p => p.FilePath).ToList());
                    if (Start(ChannelTranslation))
                    {
                        SelectedCharacter.Running = true;
                        Running = true;
                        ShowSuccess(Helpers.ResourcesHelper.GetString("Channel_Started"));
                    }
                }
                else
                {
                    ShowError(Helpers.ResourcesHelper.GetString("Channel_NoChannel"));
                }
            }
        });

        public ICommand StartAllCommand => new RelayCommand(() =>
        {
            foreach (var character in Characters)
            {
                if (character.Running)
                {
                    continue;
                }
                var channelMarket = GetChannelTranslation(character.Name);
                if (Start(channelMarket))
                {
                    character.Running = true;
                    Running = true;
                }
            }
        });
        public ICommand StopCommand => new RelayCommand(() =>
        {
            Stop(ChannelTranslation);
            SelectedCharacter.Running = false;
            Running = Characters.FirstOrDefault(p => p.Running) != null;
        });
        public ICommand StopAllCommand => new RelayCommand(() =>
        {
            StopAll();
            Running = false;
        });
        public ICommand RestorePosCommand => new RelayCommand(() =>
        {
            ChannelMarketService.Current.RestorePos();
        });
        public ICommand RefreshChannelsCommand => new RelayCommand(async () =>
        {
            string name = SelectedCharacter.Name;
            ShowWaiting();
            var channels = await Task.Run(() =>
            {
                var dic = GameLogHelper.GetChatChanelInfos(_logPath, Services.Settings.GameLogsSettingService.EVELogsChannelDurationValue);
                if (dic.TryGetValue(name, out var list))
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
            _listenerChannelDic.Remove(name);
            _listenerChannelDic.Add(name, channels);
            foreach (var channel in channels)
            {
                channel.IsChecked = ChannelTranslation.Setting.Channels.Contains(channel.FilePath);
            }
            ChatChanelInfos = channels;
            HideWaiting();
        });
        public ICommand RefreshCharactersCommand => new RelayCommand(async () =>
        {
            ShowWaiting();
            await LoadListenerChannelDic();
            if (_listenerChannelDic.Any())
            {
                foreach (var item in _listenerChannelDic)
                {
                    if (Characters.FirstOrDefault(p => p.Name == item.Key) == null)
                    {
                        Characters.Add(new ChannelListener(item.Key));
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
                Title = Helpers.ResourcesHelper.GetString("Channel_ApplySettingToAll"),
                Content = new Microsoft.UI.Xaml.Controls.TextBlock()
                {
                    Text = Helpers.ResourcesHelper.GetString("Channel_ApplySettingToAll_Tip")
                },
                PrimaryButtonText = Helpers.ResourcesHelper.GetString("General_OK"),
                CloseButtonText = Helpers.ResourcesHelper.GetString("General_Cancel"),
            };
            if (await contentDialog.ShowAsync() == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                StopAll();
                foreach (var character in Characters)
                {
                    var channel = GetChannelTranslation(character.Name);
                    if (channel != null && channel != ChannelTranslation)
                    {
                        channel.Setting.Keyword = channel.Setting.Keyword;
                        channel.Setting.SkipMyself = channel.Setting.SkipMyself;
                        channel.Setting.AutoTranslateFrom = channel.Setting.AutoTranslateFrom;
                        channel.Setting.AutoTranslateTo = channel.Setting.AutoTranslateTo;
                        channel.Save();
                    }
                }
                ShowSuccess(Helpers.ResourcesHelper.GetString("Channel_ApplySettingToAll_Succes"));
            }
        });
        public void Dispose()
        {
            StopAll();
            ClientServiceHelper.GetRequiredService<ChannelTranslationService>().Stop();
        }
    }
}
