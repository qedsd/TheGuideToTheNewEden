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
                item.Code = values.Length >= 2 ? int.Parse(values[1]) : throw new Exception("未知按键Code");
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
