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
    internal class EarlyWarningItemViewModel : ObservableObject
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
            await Task.Run(() =>
            {
                var allFiles = System.IO.Directory.GetFiles(LogPath);
                foreach (var file in allFiles)
                {
                    var chanelInfo = GameLogHelper.GetChatChanelInfo(file);
                    if (chanelInfo != null)
                    {
                        if (ListenerChannelDic.TryGetValue(chanelInfo.Listener, out List<ChatChanelInfo> channels))
                        {
                            channels.Add(ChatChanelInfo.Create(chanelInfo));
                        }
                        else
                        {
                            ListenerChannelDic.Add(chanelInfo.Listener, channels);
                        }
                    }
                }
            });
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
    }
}
