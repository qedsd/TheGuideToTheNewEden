using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.Core.Models.Translation;

namespace TheGuideToTheNewEden.Core.Models.Channel.Translation
{
    public class ChannelTranslationResult
    {
        public ChatContent ChatContent { get; set; }
        public TranslationResult TranslationResult { get; set; }
    }
}
