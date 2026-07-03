using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Mail
{
    public class MailLabel
    {
        public string Color { get; set; }
        public int Label_id { get; set; }
        public string Name { get; set; }
        public int Unread_count { get; set; }

        /// <summary>
        /// XAML 绑定用的 PascalCase 属性
        /// </summary>
        public long LabelId => Label_id;
        public int UnreadCount => Unread_count;

        public static MailLabel FromESI(ESI.NET.Models.Mail.Label label) => new MailLabel
        {
            Color = label.Color,
            Label_id = (int)label.LabelId,
            Name = label.Name,
            Unread_count = label.UnreadCount,
        };
    }
}
