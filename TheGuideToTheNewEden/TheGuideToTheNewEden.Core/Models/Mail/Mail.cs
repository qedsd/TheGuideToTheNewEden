using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.Mail
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
        public string RecipientsStr
        {
            get
            {
                if(Recipients == null)
                {
                    return null;
                }
                else
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    foreach(var recipient in Recipients)
                    {
                        if (string.IsNullOrEmpty(recipient.Recipient_name))
                        {
                            stringBuilder.Append(recipient.Recipient_id);
                        }
                        else
                        {
                            stringBuilder.Append(recipient.Recipient_name);
                        }
                        stringBuilder.Append(";");
                    }
                    stringBuilder.Remove(stringBuilder.Length - 1, 1);
                    return stringBuilder.ToString();
                }
            }
        }

        public string H5Body
        {
            get => Helpers.GameTextToH5Helper.TranFormat(Body);
        }
    }
}
