using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.UWP.Core.Models.Mail
{
    public class Mail
    {
        public string Body { get; set; }
        public int From { get; set; }
        public string From_name { get; set; }
        public bool Is_read{get;set; }
        public List<int> Labels { get; set; }
        public int Mail_id { get; set; }
        public List<Recipients> Recipients { get; set; }
        public string Subject { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
