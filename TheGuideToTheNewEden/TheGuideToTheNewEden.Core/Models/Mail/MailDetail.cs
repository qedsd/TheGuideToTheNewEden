using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Mail
{
    public class MailDetail
    {
        public Header Header { get; set; }
        public ESI.NET.Models.Mail.Message Message { get; set; }
        public string Labels { get; set; }
        public DateTime DateTime { get; set; }
        public MailDetail(ESI.NET.Models.Mail.Message message)
        {
            Message = message;
            DateTime = DateTime.Parse(message.Timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }
    }
}
