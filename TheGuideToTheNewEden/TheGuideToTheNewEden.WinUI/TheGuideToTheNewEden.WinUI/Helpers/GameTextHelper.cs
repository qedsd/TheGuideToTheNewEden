using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.EVELogs;

namespace TheGuideToTheNewEden.WinUI.Helpers
{
    public static class GameTextHelper
    {
        public static Paragraph ToParagraph(string text)
        {
            Paragraph paragraph = new Paragraph();
            if(!string.IsNullOrEmpty(text))
            {
                var array1 = text.Split("<a");
                if(array1.Length > 0)
                {
                    foreach(var str in array1)
                    {
                        int startTagEndIndex = str.IndexOf(">");
                        string removedStartTagText = str.Substring(startTagEndIndex + 1);
                        var array2 = removedStartTagText.Split("</a>");
                        if(array2.Length == 2)
                        {
                            //前部分为标签内，后部分为标签外
                            Run tagRun = new Run()
                            {
                                FontWeight = FontWeights.Medium,
                                Text = $" {array2[0]} "
                            };
                            Run otherRun = new Run()
                            {
                                Text = array2[1]
                            };
                            paragraph.Inlines.Add(tagRun);
                            paragraph.Inlines.Add(otherRun);
                        }
                        else
                        {
                            Run otherRun = new Run()
                            {
                                Text = removedStartTagText
                            };
                            paragraph.Inlines.Add(otherRun);
                        }
                    }
                }
                else
                {
                    paragraph.Inlines.Add(new Run() { Text = text });
                }
            }
            return paragraph;
        }
    }
}
