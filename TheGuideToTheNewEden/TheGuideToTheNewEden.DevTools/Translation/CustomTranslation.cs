using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.DevTools.Translation
{
    internal class CustomTranslation
    {
        public string Zh {  get; set; }
        public string[] Ens {  get; set; }
        public CustomTranslation(string zh, params string[] ens)
        {
            Zh = zh;
            Ens = ens;
        }
    }
}
