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
using TheGuideToTheNewEden.Core.DBModels;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class BackstoryPage : Page
    {
        public BackstoryPage()
        {
            this.InitializeComponent();
        }

        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            VM.SelectedBackstory = args.SelectedItem as Backstory;
            if (VM.SelectedBackstory.Title_Zh.Contains(sender.Text))
            {
                sender.Text = VM.SelectedBackstory.Title_Zh;
            }
            else
            {
                sender.Text = VM.SelectedBackstory.Title_En;
            }
        }

        private async void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (VM.SelectedBackstory != null)
            {
                if (VM.SelectedBackstory.Title_Zh == sender.Text || VM.SelectedBackstory.Title_En == sender.Text)
                {
                    return;
                }
            }
            if (string.IsNullOrEmpty(sender.Text))
            {
                sender.ItemsSource = null;
            }
            else
            {
                sender.ItemsSource = await Core.Services.DB.BackstoryService.QueryBackstoryAsync(sender.Text);
            }
        }
    }
}
