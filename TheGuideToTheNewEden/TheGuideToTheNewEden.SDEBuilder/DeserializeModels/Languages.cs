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
            string name = En;
            switch (language)
            {
                case LanguageEnum.De: name = De; break;
                case LanguageEnum.En: name = En; break;
                case LanguageEnum.Es: name = Es; break;
                case LanguageEnum.Fr: name = Fr; break;
                case LanguageEnum.Ja: name = Ja; break;
                case LanguageEnum.Ko: name = Ko; break;
                case LanguageEnum.Ru: name = Ru; break;
                case LanguageEnum.Zh: name = Zh; break;
                default: name = En;break;
            }
            name = string.IsNullOrEmpty(name) ? En : name;
            return name;
        }
    }
}
