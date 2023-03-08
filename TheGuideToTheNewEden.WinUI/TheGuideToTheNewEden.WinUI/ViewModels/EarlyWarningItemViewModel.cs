using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Shapes;
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
using TheGuideToTheNewEden.WinUI.Models;
using TheGuideToTheNewEden.WinUI.Services;
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

        internal EarlyWarningItemViewModel()
        {
            LogPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"EVE", "logs", "Chatlogs");
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
        public List<ChatChanelInfo> ChatChanelInfos
        {
            get => chatChanelInfos;
            set => SetProperty(ref chatChanelInfos, value);
        }

        private ObservableCollection<string> chatContents = new ObservableCollection<string>();
        public ObservableCollection<string> ChatContents
        {
            get => chatContents;
            set => SetProperty(ref chatContents, value);
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

        private void UpdateSelectedCharacter()
        {
            if(ListenerChannelDic.TryGetValue(selectedCharacter,out var chatChanelInfos))
            {
                ChatChanelInfos = chatChanelInfos;
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

        public ICommand StartCommand => new RelayCommand(() =>
        {
            if(ChatChanelInfos.NotNullOrEmpty())
            {
                foreach(var ch in ChatChanelInfos.Where(p=>p.IsChecked))
                {
                    Core.Models.EarlyWarningItem earlyWarningItem = new Core.Models.EarlyWarningItem(ch);
                    earlyWarningItem.OnContentUpdate += EarlyWarningItem_OnContentUpdate;
                    earlyWarningItem.OnWarningUpdate += EarlyWarningItem_OnWarningUpdate;
                    Core.Services.ObservableFileService.Add(earlyWarningItem);
                    earlyWarningItem.Update();
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
                WarningService.NotifyWindow(ch);
                WarningService.NotifyPopupToast(ch);
            }
        }
        /// <summary>
        /// 频道内容更新
        /// </summary>
        /// <param name="earlyWarningItem"></param>
        /// <param name="newlines"></param>
        private void EarlyWarningItem_OnContentUpdate(Core.Models.EarlyWarningItem earlyWarningItem, IEnumerable<string> newlines)
        {
            Helpers.WindowHelper.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                foreach (var line in newlines)
                {
                    ChatContents.Add(line);
                }
            });
        }

        public ICommand StopCommand => new RelayCommand(() =>
        {

        });
    }
}
