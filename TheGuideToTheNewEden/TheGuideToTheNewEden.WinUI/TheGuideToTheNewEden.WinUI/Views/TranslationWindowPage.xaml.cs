using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WinUI.Interfaces;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class TranslationWindowPage : Page
    {
        public TranslationWindowPage()
        {
            this.InitializeComponent();
        }
        public void SetWindow(IWindow window)
        {
            VM.SetWindow(window);
        }

        private void InputBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            VM.SearchText = sender.Text;
        }

        private bool _fromChosen = false;
        private void InputBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            _fromChosen = true;
            VM.SelectedDataBaseSearchItem = args.SelectedItem as DataBaseSearchItem;
        }

        private void InputBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if(_fromChosen)
            {
                _fromChosen = false;
                return;
            }
            VM.SearchText = sender.Text;
            VM.Translation();
        }
    }
}
