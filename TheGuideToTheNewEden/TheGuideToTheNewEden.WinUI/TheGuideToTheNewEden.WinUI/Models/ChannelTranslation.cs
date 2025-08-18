using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.Core.Models.Channel.Translation;

namespace TheGuideToTheNewEden.WinUI.Models
{
    public class ChannelTranslation
    {
        public ChannelTranslationSetting Setting { get; private set; }
        private List<ChannelTranslationObserver> _observers;
        private ChannelTranslationService _translationService;

        public ChannelTranslation(string characterName)
        {
            Setting = ChannelTranslationSettingService.GetValue(characterName);
            Setting ??= new ChannelTranslationSetting()
                {
                    CharacterName = characterName
                };
            _translationService = ClientServiceHelper.GetRequiredService<ChannelTranslationService>();
        }
        public void SetSelectedChannels(List<string> paths)
        {
            Setting.Channels = paths;
        }
        public void Start()
        {
            _observers ??= new List<ChannelTranslationObserver>();
            _observers.Clear();
            foreach (var path in Setting.Channels)
            {
                ChannelTranslationObserver observer = new ChannelTranslationObserver(path, Setting.CharacterName, Setting.Keyword);
                if (Core.Services.ObservableFileService.Add(observer))
                {
                    observer.OnContentUpdate += Observer_OnContentUpdate;
                    _observers.Add(observer);
                }
            }
            Save();
        }

        private void Observer_OnContentUpdate(object sender, IEnumerable<Core.Models.EVELogs.ChatContent> news)
        {
            if (news != null)
            {
                _translationService.Query(news.Where(p => p.Important), Setting.AutoTranslateFrom, Setting.AutoTranslateTo);
            }
        }

        public void Stop()
        {
            Core.Services.ObservableFileService.Remove(_observers);
            _observers.Clear();
        }
        public void Save()
        {
            ChannelTranslationSettingService.SetValue(Setting);
        }
    }
}
