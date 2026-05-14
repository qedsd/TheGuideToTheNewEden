using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.DevTools.Translation
{
    internal class TranslationItem
    {
        public TranslationItem(string en, string zh)
        {
            En = en;
            Zh = zh;
        }
        public string En { get; set; }
        public string Zh { get; set; }
    }
}
