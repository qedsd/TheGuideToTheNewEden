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
using System.Reflection.PortableExecutable;
using TheGuideToTheNewEden.WinUI.Wins;
using TheGuideToTheNewEden.WinUI.Converters;
using System.Text;
using TheGuideToTheNewEden.WinUI.Extensions;
using CoreMail = TheGuideToTheNewEden.Core.Models.Mail;
using EVEStandard.Models.API;
using EVEStandard;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class MailPage : Page,ICharacterPage
    {
        private EVEStandardAPI _esiClient;
        private AuthDTO _auth;
        private Core.Models.Character.AuthorizedCharacterData _characterData;
        public MailPage()
        {
            this.InitializeComponent();
            Loaded += MailPage_Loaded;
        }

        private void MailPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded)
            {
                Refresh();
                _isLoaded = true;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var paras = e.Parameter as object[];
            if (paras != null && paras.Length == 2)
            {
                _esiClient = paras[0] as EVEStandardAPI;
                _characterData = paras[1] as Core.Models.Character.AuthorizedCharacterData;
                _auth = _characterData.ToAuthDTO();
            }
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
            this.ShowWaiting();
            var labelsResp = await _esiClient.Mail.GetMailLabelsAndUnreadCountsAsync(_auth);
            if(labelsResp?.Model != null)
            {
                var labelsList = labelsResp.Model?.Labels?.Select(CoreMail.MailLabel.FromESI).ToList();
                RenameLabel(labelsList);
                ListView_Label.ItemsSource = labelsList;
            }
            //var maillistResp = await _esiClient.Mail.MailingLists();
            //if (maillistResp != null && maillistResp.StatusCode == System.Net.HttpStatusCode.OK)
            //{
            //    ListView_MailList.ItemsSource = maillistResp.Data;
            //}
            this.HideWaiting();
        }

        private void RenameLabel(List<CoreMail.MailLabel> labels)
        {
            if(labels.NotNullOrEmpty())
            {
                foreach (var label in labels)
                {
                    label.Name = Helpers.ResourcesHelper.GetString(label.Name);
                }
            }
        }

        private async void ListView_Label_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView_Label.SelectedItem != null)
            {
                var label = ListView_Label.SelectedItem as CoreMail.MailLabel;
                if (label != null)
                {
                    this.ShowWaiting();
                    var headers = await _esiClient.Mail.ReturnMailHeadersAsync(_auth,new List<long> { label.LabelId }, default);
                    this.HideWaiting();
                    if (headers?.Model != null)
                    {
                        var list = headers.Model.Select(p => new Core.Models.Mail.Header(p)).ToList();
                        var nameResp = await _esiClient.Universe.GetNamesAndCategoriesFromIdsAsync(headers.Model.Select(p=>p.From.Value).Distinct().ToList());
                        if (nameResp ?.Model != null)
                        {
                            var namesDic = nameResp.Model.ToDictionary(p => p.Id);
                            foreach (var item in list)
                            {
                                if(namesDic.TryGetValue(item.From.Value, out var value))
                                {
                                    item.FromName = value.Name;
                                    item.Category = Enum.Parse< EVEStandard.Enumerations.CategoryEnum>(value.Category);
                                }
                                else
                                {
                                    item.FromName = item.From.ToString();
                                    item.Category = EVEStandard.Enumerations.CategoryEnum.character;//default
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
                        this.ShowError($"ReturnMailHeadersAsync Failed:{label.LabelId}");
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
                this.ShowWaiting();
                var msgResp = await _esiClient.Mail.ReturnMailAsync(_auth,header.MailId.Value);
                this.HideWaiting();
                if (msgResp?.Model != null)
                {
                    var mailDetail = new Core.Models.Mail.MailDetail(msgResp.Model);
                    mailDetail.Header = header;
                    if(mailDetail.Message.Labels.NotNullOrEmpty())
                    {
                        StringBuilder stringBuilder = new StringBuilder();
                        var labels = ListView_Label.ItemsSource as List<CoreMail.MailLabel>;
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
                    if(!header.IsReadForUI)
                    {
                        await _esiClient.Mail.UpdateMetadataAboutMailAsync(_auth, header.MailId.Value, new EVEStandard.Models.UpdateMailMetadata() { Read = true });
                        header.IsReadForUI = true;
                    }
                }
                else
                {
                    this.ShowError($"ReturnMailAsync Failed:{header.MailId.Value}");
                }
            }
        }
    }
}
