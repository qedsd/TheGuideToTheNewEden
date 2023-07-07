using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models
{
    public class OfflineMonitorSetting : ObservableObject
    {
        public List<int> MonitoringCharacterIds { get; set; } = new List<int>();
        private bool systemNotify = true;
        /// <summary>
        /// 系统消息通知
        /// </summary>
        public bool SystemNotify
        {
            get => systemNotify; set => SetProperty(ref systemNotify, value);
        }

        private bool mailNotify = false;
        /// <summary>
        /// 邮件通知
        /// </summary>
        public bool MailNotify
        {
            get => mailNotify; set => SetProperty(ref mailNotify, value);
        }

        private string fromMailAddr;
        public string FromMailAddr { get => fromMailAddr; set => SetProperty(ref fromMailAddr, value); }

        private string fromName;
        public string FromName { get => fromName; set => SetProperty(ref fromName, value); }

        private string fromPassword;
        public string FromPassword { get => fromPassword; set => SetProperty(ref fromPassword, value); }

        private string smtpHost = "smtp.qq.com";
        public string SmtpHost { get => smtpHost; set => SetProperty(ref smtpHost, value); }

        private int port = 587;
        public int Port { get => port; set => SetProperty(ref port, value); }

        private string toMailAddr;
        public string ToMailAddr { get => toMailAddr; set => SetProperty(ref toMailAddr, value); }


    }
}
