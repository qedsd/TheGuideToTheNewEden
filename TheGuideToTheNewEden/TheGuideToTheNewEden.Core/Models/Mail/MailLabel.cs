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
        public long Unread_count { get; set; }

        /// <summary>
        /// XAML 绑定用的 PascalCase 属性
        /// </summary>
        public long LabelId => Label_id;
        public long UnreadCount => Unread_count;

        public static MailLabel FromESI(EVEStandard.Models.MailLabel label) => new MailLabel
        {
            Color = label.Color,
            Label_id = (int)label.LabelId,
            Name = label.Name,
            Unread_count = label.UnreadCount == null ? 0 : label.UnreadCount.Value,
        };
    }
}
