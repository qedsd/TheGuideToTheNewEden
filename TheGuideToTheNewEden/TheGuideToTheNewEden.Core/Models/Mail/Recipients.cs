using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Mail
{
    /// <summary>
    /// 收件人
    /// </summary>
    public class Recipients
    {
        public int Recipient_id { get; set; }
        public string Recipient_type { get; set; }
        public string Recipient_name { get; set; }
    }
}
