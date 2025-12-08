using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class Languages
    {
        public string De { get; set; }
        public string En { get; set; }
        public string Es { get; set; }
        public string Fr { get; set; }
        public string Ja { get; set; }
        public string Ko { get; set; }
        public string Ru { get; set; }
        public string Zh { get; set; }
        public string GetValue(LanguageEnum language)
        {
            switch (language)
            {
                case LanguageEnum.De:return De;
                case LanguageEnum.En:return En;
                case LanguageEnum.Es:return Es;
                case LanguageEnum.Fr:return Fr;
                case LanguageEnum.Ja: return Ja;
                case LanguageEnum.Ko: return Ko;
                case LanguageEnum.Ru: return Ru;
                case LanguageEnum.Zh: return Zh;
                default: return En;
            }
        }
    }
}
