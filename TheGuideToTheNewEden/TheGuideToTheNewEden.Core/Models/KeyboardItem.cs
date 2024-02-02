using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models
{
    /// <summary>
    /// 键盘按键
    /// </summary>
    public class KeyboardItem
    {
        public string Name { get; set; }
        public int Code { get; set; }
        public string Note { get; set; }

        public static KeyboardItem FromCsv(string csvLine)
        {
            try
            {
                string[] values = csvLine.Split(',');
                KeyboardItem item = new KeyboardItem();
                item.Name = values[0];
                if(values.Length > 1)
                {
                    if(values[1].StartsWith("0x"))
                    {
                        item.Code = Convert.ToInt32(values[1], 16);
                    }
                    else
                    {
                        item.Code = int.Parse(values[1]);
                    }
                }
                else
                {
                    throw new Exception("未知按键Code");
                }
                item.Note = values.Length >= 3 ? values[2] : null;
                return item;
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                return null;
            }
        }
    }
}
