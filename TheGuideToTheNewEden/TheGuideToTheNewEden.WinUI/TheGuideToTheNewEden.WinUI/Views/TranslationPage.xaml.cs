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
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Documents;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class TranslationPage : Page
    {
        private SearchInvType _invType;
        public TranslationPage()
        {
            this.InitializeComponent();
        }

        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            _invType = args.SelectedItem as SearchInvType;
            sender.Text = _invType.TypeName;
            ShowDetail();
        }

        private async void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (_invType?.TypeName == sender.Text)
            {
                return;
            }
            if (string.IsNullOrEmpty(sender.Text))
            {
                sender.ItemsSource = null;
            }
            else
            {
                sender.ItemsSource = await Core.Services.DB.InvTypeService.SearchTypeAsync(sender.Text);
            }
        }
        private void ShowDetail()
        {
            Paragraph paragraph = new Paragraph()
            {
                Margin = new Thickness(0, 8, 0, 8),
            };
            Run enRun = new Run();
            Run localRun = new Run();
            paragraph.Inlines.Add(enRun);
            paragraph.Inlines.Add(localRun);
            Image_Type.Source = new BitmapImage(new Uri(Converters.GameImageConverter.GetImageUri(_invType.TypeID, Converters.GameImageConverter.ImgType.Type, 64)));
            if(_invType.IsLocal)
            {
                var mainType = Core.Services.DB.InvTypeService.QueryType(_invType.TypeID, false);
                if (mainType != null)
                {
                    TextBlock_Name_EN.Text = mainType.TypeName;
                    enRun.Text = mainType.Description;
                }
                else
                {
                    TextBlock_Name_EN.Text = string.Empty;
                    enRun.Text = string.Empty;
                }
                TextBlock_Name_Local.Text = _invType.TypeName;
                localRun.Text = _invType.Description;
            }
            else
            {
                var localType = Core.Services.DB.LocalDbService.TranInvType(_invType.TypeID);
                if (localType != null)
                {
                    TextBlock_Name_Local.Text = localType.TypeName;
                    localRun.Text = localType.Description;
                }
                else
                {
                    TextBlock_Name_Local.Text = string.Empty;
                    localRun.Text = string.Empty;
                }
                TextBlock_Name_EN.Text = _invType.TypeName;
                enRun.Text = _invType.Description;
            }
            RichTextBlock_Desc.Blocks.Add(paragraph);
        }
    }
}
