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
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;
using ESI.NET.Models.Mail;
using System.Reflection.PortableExecutable;
using TheGuideToTheNewEden.WinUI.Wins;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class MailPage : Page,ICharacterPage
    {
        private BaseWindow _window;
        private EsiClient _esiClient;
        public MailPage()
        {
            this.InitializeComponent();
            Loaded += MailPage_Loaded;
        }

        private void MailPage_Loaded(object sender, RoutedEventArgs e)
        {
            _window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
            if (!_isLoaded)
            {
                Refresh();
                _isLoaded = true;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _esiClient = e.Parameter as EsiClient;
        }
        private bool _isLoaded = false;
        public void Clear()
        {
            _isLoaded = false;
            ListBox_Label.ItemsSource = null;
            ListBox_MailList.ItemsSource = null;
            ListBox_Mails.ItemsSource = null;
        }

        public async void Refresh()
        {
            _window?.ShowWaiting();
            var labelsResp = await _esiClient.Mail.Labels();
            if(labelsResp != null && labelsResp.StatusCode == System.Net.HttpStatusCode.OK)
            {
                RenameLabel(labelsResp.Data?.Labels);
                ListBox_Label.ItemsSource = labelsResp.Data?.Labels;
            }
            var maillistResp = await _esiClient.Mail.MailingLists();
            if (maillistResp != null && maillistResp.StatusCode == System.Net.HttpStatusCode.OK)
            {
                ListBox_MailList.ItemsSource = maillistResp.Data;
            }
            _window?.HideWaiting();
        }

        private void RenameLabel(List<ESI.NET.Models.Mail.Label> labels)
        {
            if(labels.NotNullOrEmpty())
            {
                foreach (var label in labels)
                {
                    label.Name = Helpers.StringResourcesHelper.GetString(label.Name);
                }
            }
        }

        private async void ListBox_Label_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(ListBox_Label.SelectedItem != null)
            {
                var label = ListBox_Label.SelectedItem as Label;
                if(label != null)
                {
                    _window.ShowWaiting();
                    var headers = await _esiClient.Mail.Headers(new long[] { label.LabelId });
                    _window.HideWaiting();
                    if (headers != null && headers.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        ListBox_Mails.ItemsSource = headers.Data;
                    }
                    else
                    {
                        Core.Log.Error(headers?.Message);
                        _window.ShowError(headers?.Message);
                    }
                }
            }
        }

        private async void ListBox_MailList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListBox_MailList.SelectedItem != null)
            {
                var mailingList = ListBox_MailList.SelectedItem as MailingList;
                if (mailingList != null)
                {
                    _window.ShowWaiting();
                    var headers = await _esiClient.Mail.Headers(new long[] { mailingList.MailingListId });
                    _window.HideWaiting();
                    if (headers != null && headers.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        ListBox_MailList.ItemsSource = headers.Data;
                    }
                    else
                    {
                        Core.Log.Error(headers?.Message);
                        _window.ShowError(headers?.Message);
                    }
                }
            }
        }

        private async void ListBox_Mails_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var header = ListBox_Mails.SelectedItem as Header;
            if(header != null)
            {
                _window.ShowWaiting();
                var msgResp = await _esiClient.Mail.Retrieve((int)header.MailId);
                _window.HideWaiting();
                if (msgResp != null && msgResp.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    MailWindow mailWindow = new MailWindow(_esiClient, msgResp.Data);
                    mailWindow.Activate();
                    _ =_esiClient.Mail.Update((int)header.MailId, true);
                }
                else
                {
                    Core.Log.Error(msgResp?.Message);
                    _window.ShowError(msgResp?.Message);
                }
            }
        }
    }
}
