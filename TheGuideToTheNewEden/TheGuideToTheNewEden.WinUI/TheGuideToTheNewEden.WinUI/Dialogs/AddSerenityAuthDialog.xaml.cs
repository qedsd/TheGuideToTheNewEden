// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

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
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WinUI.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Dialogs
{
    public sealed partial class AddSerenityAuthDialog : Page
    {
        public AddSerenityAuthDialog()
        {
            this.InitializeComponent();
        }
        public static async Task<string> ShowAsync(XamlRoot xamlRoot)
        {
            ContentDialog contentDialog = new ContentDialog()
            {
                XamlRoot = xamlRoot,
                Title = Helpers.ResourcesHelper.GetString("AddSerenityAuthDialog_Title"),
                Content = new AddSerenityAuthDialog(),
                PrimaryButtonText = Helpers.ResourcesHelper.GetString("General_OK"),
                CloseButtonText = Helpers.ResourcesHelper.GetString("General_Cancel"),
            };
            if (await contentDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                return (contentDialog.Content as AddSerenityAuthDialog).TextBox_Code.Text;
            }
            else
            {
                return null;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CharacterService.GetAuthorizeByBrower();
        }
    }
}
