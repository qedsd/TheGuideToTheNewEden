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
using TheGuideToTheNewEden.WinUI.Converters;
using System.Text;

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
            ListView_Label.ItemsSource = null;
            //ListView_MailList.ItemsSource = null;
            ListView_Mails.ItemsSource = null;
        }

        public async void Refresh()
        {
            _window?.ShowWaiting();
            var labelsResp = await _esiClient.Mail.Labels();
            if(labelsResp != null && labelsResp.StatusCode == System.Net.HttpStatusCode.OK)
            {
                RenameLabel(labelsResp.Data?.Labels);
                ListView_Label.ItemsSource = labelsResp.Data?.Labels;
            }
            //var maillistResp = await _esiClient.Mail.MailingLists();
            //if (maillistResp != null && maillistResp.StatusCode == System.Net.HttpStatusCode.OK)
            //{
            //    ListView_MailList.ItemsSource = maillistResp.Data;
            //}
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

        private async void ListView_Label_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView_Label.SelectedItem != null)
            {
                var label = ListView_Label.SelectedItem as Label;
                if (label != null)
                {
                    _window.ShowWaiting();
                    var headers = await _esiClient.Mail.Headers(new long[] { label.LabelId });
                    _window.HideWaiting();
                    if (headers != null && headers.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var list = headers.Data.Select(p => new Core.Models.Mail.Header(p)).ToList();
                        var nameResp = await _esiClient.Universe.Names(headers.Data.Select(p=>(long)p.From).Distinct().ToList());
                        if (nameResp != null && nameResp.StatusCode == System.Net.HttpStatusCode.OK && nameResp.Data.NotNullOrEmpty())
                        {
                            var namesDic = nameResp.Data.ToDictionary(p => p.Id);
                            foreach (var item in list)
                            {
                                if(namesDic.TryGetValue(item.From, out var value))
                                {
                                    item.FromName = value.Name;
                                    item.Category = value.Category;
                                }
                                else
                                {
                                    item.FromName = item.From.ToString();
                                    item.Category = ESI.NET.Enumerations.ResolvedInfoCategory.Character;//default
                                }
                            }
                        }
                        else
                        {
                            foreach (var item in list)
                            {
                                item.FromName = item.From.ToString();
                            }
                        }
                        ListView_Mails.ItemsSource = list;
                    }
                    else
                    {
                        Core.Log.Error(headers?.Message);
                        _window.ShowError(headers?.Message);
                    }
                }
            }
        }

        private void ListView_MailList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //if (ListView_MailList.SelectedItem != null)
            //{
            //    _window.ShowMsg("没找到相关接口，就当作没有这个功能吧");
            //    var mailingList = ListView_MailList.SelectedItem as MailingList;
            //    if (mailingList != null)
            //    {
            //        _window.ShowWaiting();
            //        var headers = await _esiClient.Mail.Headers(new long[] { mailingList.MailingListId });
            //        _window.HideWaiting();
            //        if (headers != null && headers.StatusCode == System.Net.HttpStatusCode.OK)
            //        {
            //            ListView_MailList.ItemsSource = headers.Data;
            //        }
            //        else
            //        {
            //            Core.Log.Error(headers?.Message);
            //            _window.ShowError(headers?.Message);
            //        }
            //    }
            //}
        }

        private async void ListView_Mails_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var header = ListView_Mails.SelectedItem as Core.Models.Mail.Header;
            if (header != null)
            {
                _window.ShowWaiting();
                var msgResp = await _esiClient.Mail.Retrieve((int)header.MailId);
                _window.HideWaiting();
                if (msgResp != null && msgResp.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var mailDetail = new Core.Models.Mail.MailDetail(msgResp.Data);
                    mailDetail.Header = header;
                    if(mailDetail.Message.Labels.NotNullOrEmpty())
                    {
                        StringBuilder stringBuilder = new StringBuilder();
                        var labels = ListView_Label.ItemsSource as List<ESI.NET.Models.Mail.Label>;
                        foreach (var item in  mailDetail.Message.Labels)
                        {
                            var label = labels.FirstOrDefault(p => p.LabelId == item);
                            if (label != null)
                            {
                                stringBuilder.Append(label.Name);
                                stringBuilder.Append(';');
                            }
                        }
                        if(stringBuilder.Length > 1)
                        {
                            stringBuilder.Remove(stringBuilder.Length - 1, 1);
                        }
                        mailDetail.Labels = stringBuilder.ToString();
                    }
                    MailWindow mailWindow = new MailWindow(_esiClient, mailDetail);
                    mailWindow.Activate();
                    if(!header.IsRead)
                    {
                        var updateResp = await _esiClient.Mail.Update((int)header.MailId, true);
                        if (updateResp == null || updateResp.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            Core.Log.Error(updateResp?.Message);
                        }
                        else
                        {
                            header.IsRead = true;
                        }
                    }
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
