using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.Core.Models.Channel.Translation;
using System.Threading;

namespace TheGuideToTheNewEden.WinUI.Models
{
    public class ChannelTranslation
    {
        private const int MAXCHAR = 10000;
        /// <summary>
        /// 已查询字符数量
        /// </summary>
        private static long _queryChar;
        public ChannelTranslationSetting Setting { get; private set; }
        private List<ChannelTranslationObserver> _observers;
        private ChannelTranslationService _translationService;
        private List<string> _selectedChannelPaths;

        public ChannelTranslation(string characterName)
        {
            Setting = ChannelTranslationSettingService.GetValue(characterName);
            Setting ??= new ChannelTranslationSetting()
                {
                    CharacterName = characterName
                };
            _translationService = ClientServiceHelper.GetRequiredService<ChannelTranslationService>();
        }
        public void SetSelectedChannels(List<ChatChanelInfo> channels)
        {
            Setting.Channels = channels.Select(p=>p.ChannelName).ToList();
            _selectedChannelPaths = channels.Select(p=>p.FilePath).ToList();
        }
        public void Start()
        {
            _observers ??= new List<ChannelTranslationObserver>();
            _observers.Clear();
            foreach (var path in _selectedChannelPaths)
            {
                ChannelTranslationObserver observer = new ChannelTranslationObserver(path, Setting.CharacterName, Setting.Keyword);
                if (Core.Services.ObservableFileService.Add(observer))
                {
                    observer.OnContentUpdate += Observer_OnContentUpdate;
                    _observers.Add(observer);
                }
            }
            Save();
            _translationService.Start();
        }

        private void Observer_OnContentUpdate(object sender, IEnumerable<Core.Models.EVELogs.ChatContent> news)
        {
            if (news != null)
            {
                Interlocked.Add(ref _queryChar, news.Sum(p => p.Content.Length));
                if(Interlocked.Read(ref _queryChar) > MAXCHAR)
                {
                    _translationService.ShowQueryLimited(news.Where(p => p.Important));
                }
                else
                {
                    _translationService.Query(news.Where(p => p.Important), Setting.AutoTranslateFrom, Setting.AutoTranslateTo);
                }
            }
        }

        public void Stop(string listener)
        {
            Core.Services.ObservableFileService.Remove(_observers);
            _observers.Clear();
            _translationService.Stop(listener);
        }
        public void Save()
        {
            ChannelTranslationSettingService.SetValue(Setting);
        }
    }
}
