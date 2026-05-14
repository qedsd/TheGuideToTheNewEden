using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TheGuideToTheNewEden.DevTools.Translation
{
    internal static class CreateTranslationVoca
    {
        public static async Task Start(string enToZhPath, string zhToEnPath)
        {
            var items1 = await OutputMarketName.Start();
            var items2 = OutputRegionAndSystemName.Start();
            var items3 = AddOther(true);
            var items4 = AddOther(false);
            List<TranslationItem> translationItems = new List<TranslationItem>();
            translationItems.AddRange(items1);
            translationItems.AddRange(items2);

            StringBuilder stringBuilder1 = new StringBuilder();
            StringBuilder stringBuilder2 = new StringBuilder();
            foreach (var item in translationItems)
            {
                stringBuilder1.Append(item.En);
                stringBuilder1.Append(',');
                stringBuilder1.Append(item.Zh);
                stringBuilder1.AppendLine();

                stringBuilder2.Append(item.Zh);
                stringBuilder2.Append(',');
                stringBuilder2.Append(item.En);
                stringBuilder2.AppendLine();
            }

            foreach (var item in items3)
            {
                stringBuilder1.Append(item.En);
                stringBuilder1.Append(',');
                stringBuilder1.Append(item.Zh);
                stringBuilder1.AppendLine();
            }
            foreach (var item in items4)
            {
                stringBuilder2.Append(item.Zh);
                stringBuilder2.Append(',');
                stringBuilder2.Append(item.En);
                stringBuilder2.AppendLine();
            }


            File.WriteAllText(enToZhPath, stringBuilder1.ToString());
            File.WriteAllText(zhToEnPath, stringBuilder2.ToString());
        }
        private static List<TranslationItem> AddOther(bool enToZh)
        {
            List<TranslationItem> translationItems = new List<TranslationItem>();
            List<CustomTranslation> customTranslations = new List<CustomTranslation>()
            {
                new("星系","System","system"),
                new("ISK","isk","ISK"),
                new("击杀","Kill","kill"),
                new("舰队指挥官","FC","fc"),
                new("敬礼", "07","0/"),
                new("越快越好","asap"),
                new("环绕", "Orbit","orbit"),
                new("朝向","Align","Align"),
                new("反诱导","Jammer","jammer"),
                new("诱导","Cyno","cyno"),
                new("跳跃到","Jump to","jump to","Warp to","warp to"),
                new("未看见船","NV","nv"),
                new("加力热燃烧器","AB","ab"),
                new("微型跃迁推进器","MWB","mwb"),
                new("移动式牵引","MTU", "mtu"),
                new("护卫舰","Frigate", "frigate"),
                new("航母","Carrier", "carrier"),
                new("无畏","Dreadnought", "dreadnought"),
                new("反跳/网子","Tackle", "tackle"),
                new("被抓","Tackle", "Tackled"),
                new("虫洞","J-Space"),
                new("复制图","BPC", "bcp"),
                new( "NPC怪","Rats", "rats","Rat","rat"),
                new( "刷怪","Ratting", "ratting"),
                new("技能点","SP", "sp"),
                new("多谢","Ty", "Ty"),
                new("多谢","THX", "thx"),
                new("干得漂亮","GF", "gf"),
                new("维修","Rep", "rep"),
                new("安全等级","Rec", "rec","Rec.","rec."),
                new("蓝家抓人","Awox", "awox"),
                new("隐秘行动","Covert ops", "covert ops"),
                new("隐轰","Bomber", "bomber"),
                new("","", ""),
                new("","", ""),

            };
            foreach (var item in customTranslations)
            {
                if (enToZh)
                {
                    foreach (var en in item.Ens)
                    {
                        translationItems.Add(new TranslationItem(en, item.Zh));
                    }
                }
                else
                {
                    translationItems.Add(new TranslationItem(item.Ens[0], item.Zh));//中译英仅需添加一个英文
                }
            }
            return translationItems;
        }
    }
}
