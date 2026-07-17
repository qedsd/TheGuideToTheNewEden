using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.Mail
{
    public class Header: EVEStandard.Models.Mail, INotifyPropertyChanged
    {
        public string FromName { get; set; }
        public DateTime DateTime { get; set; }
        public EVEStandard.Enumerations.CategoryEnum Category { get; set; }

        private bool isReadForUI;
        public bool IsReadForUI
        {
            get => isReadForUI;
            set
            {
                isReadForUI = value;
                NotifyPropertyChanged(nameof(IsReadForUI));
            }
        }

        public Header(EVEStandard.Models.Mail header)
        {
            this.CopyFrom(header);
            IsReadForUI = header.IsRead == true;
            DateTime = Timestamp.Value;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
