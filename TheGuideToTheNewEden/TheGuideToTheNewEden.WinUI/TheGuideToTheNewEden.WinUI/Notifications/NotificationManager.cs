using Microsoft.Windows.AppNotifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Notifications
{
    internal class NotificationManager
    {
        private bool m_isRegistered;

        private Dictionary<int, Action<AppNotificationActivatedEventArgs>> c_notificationHandlers;

        public NotificationManager()
        {
            m_isRegistered = false;

            // When adding new a scenario, be sure to add its notification handler here.
            c_notificationHandlers = new Dictionary<int, Action<AppNotificationActivatedEventArgs>>();
            //c_notificationHandlers.Add(IntelToast.ScenarioId, IntelToast.NotificationReceived);
            //c_notificationHandlers.Add(ToastWithTextBox.ScenarioId, ToastWithTextBox.NotificationReceived);
        }

        ~NotificationManager()
        {
            Unregister();
        }

        public void Init()
        {
            var notificationManager = AppNotificationManager.Default;

            // To ensure all Notification handling happens in this process instance, register for
            // NotificationInvoked before calling Register(). Without this a new process will
            // be launched to handle the notification.
            notificationManager.NotificationInvoked += OnNotificationInvoked;

            notificationManager.Register();
            m_isRegistered = true;
        }

        public void Unregister()
        {
            if (m_isRegistered)
            {
                AppNotificationManager.Default.Unregister();
                m_isRegistered = false;
            }
        }

        public void ProcessLaunchActivationArgs(AppNotificationActivatedEventArgs notificationActivatedEventArgs)
        {
            DispatchNotification(notificationActivatedEventArgs);
            //NotifyUser.AppLaunchedFromNotification();//应用内通知
        }

        /// <summary>
        /// 发送通知到对应类型Toast进行处理
        /// </summary>
        /// <param name="notificationActivatedEventArgs"></param>
        /// <returns></returns>
        public bool DispatchNotification(AppNotificationActivatedEventArgs notificationActivatedEventArgs)
        {
            var scenarioId = notificationActivatedEventArgs.Arguments[Common.scenarioTag];
            if (scenarioId.Length != 0)
            {
                try
                {
                    c_notificationHandlers[int.Parse(scenarioId)](notificationActivatedEventArgs);
                    return true;
                }
                catch
                {
                    return false; // Couldn't find a NotificationHandler for scenarioId.
                }
            }
            else
            {
                return false; // No scenario specified in the notification
            }
        }
        /// <summary>
        /// 点击通知第一步响应的地方
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="notificationActivatedEventArgs"></param>
        void OnNotificationInvoked(object sender, AppNotificationActivatedEventArgs notificationActivatedEventArgs)
        {
            //应用内通知
            //NotifyUser.NotificationReceived();

            if (!DispatchNotification(notificationActivatedEventArgs))
            {
                //NotifyUser.UnrecognizedToastOriginator();
            }
        }
    }
}
