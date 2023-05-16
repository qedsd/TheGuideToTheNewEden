using ESI.NET;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class MailDetailPage : Page
    {
        private BaseWindow _window;
        private EsiClient _esiClient;
        private ESI.NET.Models.Mail.Message _message;
        public MailDetailPage(EsiClient esiClient, ESI.NET.Models.Mail.Message message)
        {
            _esiClient = esiClient;
            _message = message;
            this.InitializeComponent();
            Loaded += MailDetailPage_Loaded;
        }

        private void MailDetailPage_Loaded(object sender, RoutedEventArgs e)
        {
            _window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
            Task.Run(() =>
            {
                //TODO:获取发件人收件人姓名
                _window.DispatcherQueue.TryEnqueue(() =>
                {

                });
            });
        }

        public void SetFromName(string name)
        {

        }

        public void SetRecipientNames(string names)
        {

        }
    }
}
