using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.UWP.Core.Models.Mail
{
    public class MailLabelRoot
    {
        public List<MailLabel> Labels { get; set; }
        public int Total_unread_count { get; set; }
    }
}
