using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Models.Translation;

namespace TheGuideToTheNewEden.Core.Interfaces
{
    public interface ITranslationService
    {
        System.Threading.Tasks.Task<TranslationResult> Translate(string text, string from, string to);
    }
}
