using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Extensions;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.Market;
using static Vanara.PInvoke.Kernel32.DEVICE_MEDIA_INFO.DEVICESPECIFIC;

namespace TheGuideToTheNewEden.WinUI.Dialogs
{
    public sealed partial class EditLinkInfoDialog : Page
    {
        private LinkInfo _linkInfo;
        private EditLinkInfoDialog(LinkInfo linkInfo)
        {
            _linkInfo = linkInfo;
            this.InitializeComponent();
            TextBox_Name.Text = linkInfo.Name;
            TextBox_Url.Text = linkInfo.Url;
            TextBox_ShortDescription.Text = linkInfo.ShortDescription;
            TextBox_Description.Text = linkInfo.Description;
            TextBox_IconUrl.Text = linkInfo.IconUrl;
            TextBox_Langs.Text = linkInfo.GetLangs(',');
            TextBox_Platforms.Text = linkInfo.GetPlatforms(',');
            TextBox_Categories.Text = linkInfo.GetCategories(',');
        }
        public void Set()
        {
            _linkInfo.Name = TextBox_Name.Text;
            _linkInfo.Url = TextBox_Url.Text;
            _linkInfo.ShortDescription = TextBox_ShortDescription.Text;
            _linkInfo.Description = TextBox_Description.Text;
            _linkInfo.IconUrl = TextBox_IconUrl.Text;
            _linkInfo.Langs = TextBox_Langs.Text.Split(',');
            _linkInfo.Platforms = TextBox_Platforms.Text.Split(',');
            _linkInfo.Categories = TextBox_Categories.Text.Split(',');
        }
        public static async Task<LinkInfo> AddAsync(XamlRoot xamlRoot)
        {
            LinkInfo linkInfo = null;
            await ShowAsync(linkInfo, xamlRoot);
            return linkInfo;
        }
        public static async Task<bool> EditAsync(LinkInfo linkInfo, XamlRoot xamlRoot)
        {
            return await ShowAsync(linkInfo, xamlRoot);
        }
        public static async Task<bool> ShowAsync(LinkInfo linkInfo, XamlRoot xamlRoot)
        {
            LinkInfo link;
            if (linkInfo != null)
            {
                link = linkInfo.DepthClone<LinkInfo>();
            }
            else
            {
                link = new LinkInfo();
            }
            EditLinkInfoDialog content = new EditLinkInfoDialog(link);
            ContentDialog contentDialog = new ContentDialog()
            {
                XamlRoot = xamlRoot,
                Title = linkInfo == null ? Helpers.ResourcesHelper.GetString("LinksPage_Add") : Helpers.ResourcesHelper.GetString("LinksPage_Edit"),
                Content = content,
                PrimaryButtonText = Helpers.ResourcesHelper.GetString("General_OK"),
                CloseButtonText = Helpers.ResourcesHelper.GetString("General_Cancel"),
            };
            if (await contentDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                content.Set();
                if(linkInfo != null)
                {
                    linkInfo.CopyFrom(link);
                }
                else
                {
                    linkInfo = link;
                }
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
