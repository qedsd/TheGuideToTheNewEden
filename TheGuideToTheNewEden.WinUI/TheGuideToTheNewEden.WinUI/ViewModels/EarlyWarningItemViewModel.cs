using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.WinUI.Models;

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
                            //if(onlyOneChanels.TryGetValue(chanelInfo.ChannelID,out var chanel))
                            //{
                            //    if(chanel.SessionStarted < chanelInfo.SessionStarted)
                            //    {
                            //        onlyOneChanels.Remove(chanelInfo.ChannelID);
                            //        onlyOneChanels.Add(chanelInfo.ChannelID,chanelInfo);
                            //    }
                            //}
                            //else
                            //{
                            //    onlyOneChanels.Add(chanelInfo.ChannelID, chanelInfo);
                            //}
                            if (ListenerChannelDic.TryGetValue(chanelInfo.Listener, out List<ChatChanelInfo> channels))
                            {
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
                    //foreach(var chanelInfo in onlyOneChanels.Values)
                    //{
                    //    if (ListenerChannelDic.TryGetValue(chanelInfo.Listener, out List<ChatChanelInfo> channels))
                    //    {
                    //        channels.Add(ChatChanelInfo.Create(chanelInfo));
                    //    }
                    //    else
                    //    {
                    //        channels = new List<ChatChanelInfo>()
                    //            {
                    //                ChatChanelInfo.Create(chanelInfo)
                    //            };
                    //        ListenerChannelDic.Add(chanelInfo.Listener, channels);
                    //    }
                    //}
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
            
        });

        public ICommand StopCommand => new RelayCommand(() =>
        {

        });
    }
}
