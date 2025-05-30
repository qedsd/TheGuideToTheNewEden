using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using TheGuideToTheNewEden.WinUI.Models;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WinUI.Services;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using System.Xml.Linq;
using System.Threading.Channels;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class ChannelMarketViewModel : BaseViewModel, IDisposable
    {
        private readonly string _logPath;
        /// <summary>
        /// key为角色名
        /// value为角色下所有的不重复频道
        /// 每个频道都是该频道最新日期的那个文件
        /// </summary>
        private readonly Dictionary<string, List<ChatChanelInfo>> _listenerChannelDic = new Dictionary<string, List<ChatChanelInfo>>();
        private readonly Dictionary<string, ChannelMarket> _channelMarkets = new Dictionary<string, ChannelMarket>();

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

        private ChannelMarket _channelMarket;
        public ChannelMarket ChannelMarket
        {
            get => _channelMarket;
            set
            {
                SetProperty(ref _channelMarket, value);
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

        private MapRegion selectedRegion;
        public MapRegion SelectedRegion
        {
            get => selectedRegion;
            set
            {
                if (SetProperty(ref selectedRegion, value))
                {
                    if(value != null)
                    {
                        SelectedRegionName = value.RegionName;
                        ChannelMarket.Setting.MarketRegionID = value.RegionID;
                    }
                }
            }
        }

        private string selectedRegionName;
        public string SelectedRegionName
        {
            get => selectedRegionName;
            set
            {
                SetProperty(ref selectedRegionName, value);
            }
        }

        public ChannelMarketViewModel()
        {
            _logPath = System.IO.Path.Combine(GameLogsSettingService.EVELogsPathValue, "Chatlogs");
            ChannelMarketService.Current.Start();
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
        private void UpdateSelectedCharacter(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                ChannelMarket = null;
                ChatChanelInfos = null;
            }
            else
            {
                ChannelMarket = GetChannelMarket(name);
                ChatChanelInfos = _listenerChannelDic[name];
                foreach (var channel in ChatChanelInfos)
                {
                    channel.IsChecked = ChannelMarket.Setting.Channels.Contains(channel.FilePath);
                }
                SelectedRegionName = Core.Services.DB.MapRegionService.Query(ChannelMarket.Setting.MarketRegionID)?.RegionName;
            }
        }
        private ChannelMarket GetChannelMarket(string name)
        {
            if (_channelMarkets.TryGetValue(name, out var channelIntel))
            {
                return channelIntel;
            }
            else
            {
                ChannelMarket channelMarket = new ChannelMarket(name);
                _channelMarkets.Add(name, channelMarket);
                return channelMarket;
            }
        }

        private bool Start(ChannelMarket channelMarket)
        {
            if (channelMarket != null && channelMarket.Setting.Channels.NotNullOrEmpty())
            {
                try
                {
                    channelMarket.Start();
                    return true;
                }
                catch (Exception ex)
                {
                    Core.Log.Error(ex);
                    Window?.ShowError(ex.Message);
                    return false;
                }
            }
            else
            {
                Window?.ShowError(Helpers.ResourcesHelper.GetString("ChannelMarket_NoChannel"));
            }
            return false;
        }

        private void Stop(ChannelMarket channelMarket)
        {
            channelMarket?.Stop();
        }
        private void StopAll()
        {
            foreach (var character in Characters)
            {
                if (character.Running)
                {
                    if (_channelMarkets.TryGetValue(character.Name, out var channelMarket))
                    {
                        channelMarket.Stop();
                    }
                    character.Running = false;
                }
            }
        }
        public ICommand StartCommand => new RelayCommand(() =>
        {
            if (ChannelMarket != null && ChatChanelInfos.NotNullOrEmpty())
            {
                var selectedChannels = ChatChanelInfos.Where(p => p.IsChecked).ToList();
                if (selectedChannels.NotNullOrEmpty())
                {
                    ChannelMarket.SetSelectedChannels(selectedChannels.Select(p => p.FilePath).ToList());
                    if (Start(ChannelMarket))
                    {
                        SelectedCharacter.Running = true;
                        Window?.ShowSuccess(Helpers.ResourcesHelper.GetString("ChannelMarket_Started"));
                    }
                }
                else
                {
                    Window?.ShowError(Helpers.ResourcesHelper.GetString("ChannelMarket_NoChannel"));
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
                var channelMarket = GetChannelMarket(character.Name);
                Start(channelMarket);
                character.Running = true;
            }
        });
        public ICommand StopCommand => new RelayCommand(() =>
        {
            Stop(ChannelMarket);
            SelectedCharacter.Running = false;
        });
        public ICommand StopAllCommand => new RelayCommand(() =>
        {
            StopAll();
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
                channel.IsChecked = ChannelMarket.Setting.Channels.Contains(channel.FilePath);
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
                Title = Helpers.ResourcesHelper.GetString("ChannelMarket_ApplySettingToAll"),
                Content = new Microsoft.UI.Xaml.Controls.TextBlock()
                {
                    Text = Helpers.ResourcesHelper.GetString("ChannelMarket_ApplySettingToAll_Tip")
                },
                PrimaryButtonText = Helpers.ResourcesHelper.GetString("General_OK"),
                CloseButtonText = Helpers.ResourcesHelper.GetString("General_Cancel"),
            };
            if (await contentDialog.ShowAsync() == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                StopAll();
                foreach (var character in Characters)
                {
                    var market = GetChannelMarket(character.Name);
                    if (market != null && market != ChannelMarket)
                    {
                        market.Setting.MarketRegionID = market.Setting.MarketRegionID;
                        market.Setting.KeyWord = market.Setting.KeyWord;
                        market.Setting.ItemsSeparator = market.Setting.ItemsSeparator;
                        market.Save();
                    }
                }
                Window?.ShowSuccess(Helpers.ResourcesHelper.GetString("ChannelMarket_ApplySettingToAll_Succes"));
            }
        });
        public void Dispose()
        {
            StopAll();
            ChannelMarketService.Current.Stop();
        }
    }
}
