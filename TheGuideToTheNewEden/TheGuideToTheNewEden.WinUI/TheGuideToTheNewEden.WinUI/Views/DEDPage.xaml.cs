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
using TheGuideToTheNewEden.Core.Models;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class DEDPage : Page
    {
        public DEDPage()
        {
            this.InitializeComponent();
        }

        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            VM.SelectedDED = args.SelectedItem as DED;
            if (VM.SelectedDED.TitleCN.Contains(sender.Text))
            {
                sender.Text = VM.SelectedDED.TitleCN;
            }
            else
            {
                sender.Text = VM.SelectedDED.TitleEN;
            }
        }

        private async void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if(VM.SelectedDED != null)
            {
                if (VM.SelectedDED.TitleCN == sender.Text || VM.SelectedDED.TitleEN == sender.Text)
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
                sender.ItemsSource = await Core.Services.DB.DEDService.SearchAsync(sender.Text);
            }
        }
    }
}
