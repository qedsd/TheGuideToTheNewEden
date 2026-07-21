using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Mail
{
    public class MailDetail
    {
        public Header Header { get; set; }
        public EVEStandard.Models.MailContent Message { get; set; }
        public string Labels { get; set; }
        public DateTime DateTime { get; set; }
        public MailDetail(EVEStandard.Models.MailContent message)
        {
            Message = message;
            DateTime = message.Timestamp.Value;
        }
    }
}
