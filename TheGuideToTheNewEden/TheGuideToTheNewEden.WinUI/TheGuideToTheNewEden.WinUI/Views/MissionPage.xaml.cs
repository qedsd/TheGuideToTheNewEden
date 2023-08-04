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

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class MissionPage : Page
    {
        public MissionPage()
        {
            this.InitializeComponent();
        }

        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            VM.SelectedMission = args.SelectedItem as Mission;
            if (VM.SelectedMission.Title_Zh.Contains(sender.Text))
            {
                sender.Text = VM.SelectedMission.Title_Zh;
            }
            else
            {
                sender.Text = VM.SelectedMission.Title_En;
            }
        }

        private async void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (VM.SelectedMission != null)
            {
                if (VM.SelectedMission.Title_Zh == sender.Text || VM.SelectedMission.Title_En == sender.Text)
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
                sender.ItemsSource = await Core.Services.DB.MissionService.QueryMissionAsync(sender.Text);
            }
        }
    }
}
