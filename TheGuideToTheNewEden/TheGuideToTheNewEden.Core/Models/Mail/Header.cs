using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.Mail
{
    public class Header: ESI.NET.Models.Mail.Header, INotifyPropertyChanged
    {
        public string FromName { get; set; }
        public DateTime DateTime { get; set; }
        public ESI.NET.Enumerations.ResolvedInfoCategory Category { get; set; }

        private bool isRead;
        public new bool IsRead
        {
            get => isRead;
            set
            {
                isRead = value;
                NotifyPropertyChanged(nameof(IsRead));
            }
        }

        public Header(ESI.NET.Models.Mail.Header header)
        {
            this.CopyFrom(header);
            IsRead = header.IsRead;
            DateTime = DateTime.Parse(Timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
