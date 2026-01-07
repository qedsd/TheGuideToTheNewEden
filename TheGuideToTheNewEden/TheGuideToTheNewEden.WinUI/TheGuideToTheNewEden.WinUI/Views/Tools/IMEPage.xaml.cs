using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;


namespace TheGuideToTheNewEden.WinUI.Views.Tools
{
    public sealed partial class IMEPage : Page, IPage
    {
        public IMEPage()
        {
            this.InitializeComponent();
        }

        public void Close()
        {
            
        }
        private bool GetSearchingPart(string fullText, out string targetText, out int startIndex, out int endIndex)
        {
            if (!string.IsNullOrEmpty(fullText) && !fullText.EndsWith(' '))
            {
                string searchWord = fullText;
                int index = fullText.LastIndexOf(' ');
                if (index > -1)
                {
                    searchWord = fullText.Substring(index);
                    startIndex = index;
                    endIndex = index;
                }
                else
                {
                    startIndex = 0;
                    endIndex = searchWord.Length - 1;
                }
                targetText = searchWord;
                return true;
            }
            else
            {
                targetText = null;
                startIndex = -1;
                endIndex = -1;
                return false;
            }
        }
        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (GetSearchingPart(InputTextBox.Text, out var targetText, out int startIndex, out int endIndex))
            {
                // 获取建议
                var suggestions = new string[] {"1211","2", "sdasasasasa2", "22121212121", "2121" };

                SuggestionsContainer.ItemsSource = suggestions;
            }
            else
            {
                SuggestionsContainer.ItemsSource = null;
            }
        }

        private void SuggestionButton_Click(object sender, RoutedEventArgs e)
        {
            if (GetSearchingPart(InputTextBox.Text, out var targetText, out int startIndex, out int endIndex))
            {

            }
        }
    }
}
