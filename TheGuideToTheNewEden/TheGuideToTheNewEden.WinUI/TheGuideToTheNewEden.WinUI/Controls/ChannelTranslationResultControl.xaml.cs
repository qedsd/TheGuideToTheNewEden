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
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.Core.Models.Channel.Translation;
using TheGuideToTheNewEden.Core.Models.Translation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class ChannelTranslationResultControl : UserControl
    {
        public ChannelTranslationResultControl()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty ViewOriginalProperty
           = DependencyProperty.Register(
               nameof(ViewOriginal),
               typeof(bool),
               typeof(ChannelTranslationResultControl),
               new PropertyMetadata(true, new PropertyChangedCallback(ViewOriginalPropertyPropertyChanged)));

        public bool ViewOriginal
        {
            get => (bool)GetValue(ViewOriginalProperty);
            set
            {
                SetValue(ViewOriginalProperty, value);
            }
        }

        private static void ViewOriginalPropertyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as ChannelTranslationResultControl;
            if (e.NewValue != null)
            {
                control.OriginalArea.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public static readonly DependencyProperty ResultProperty
           = DependencyProperty.Register(
               nameof(Result),
               typeof(ChannelTranslationResult),
               typeof(ChannelTranslationResultControl),
               new PropertyMetadata(null, new PropertyChangedCallback(ResultPropertyPropertyChanged)));

        public ChannelTranslationResult Result
        {
            get => (ChannelTranslationResult)GetValue(ResultProperty);
            set
            {
                SetValue(ResultProperty, value);
            }
        }

        private static void ResultPropertyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as ChannelTranslationResultControl;
            if (e.NewValue != null)
            {
                var value = e.NewValue as ChannelTranslationResult;
                control.Speaker.Text = value.ChatContent.SpeakerName;
                control.SpeakTime.Text = value.ChatContent.EVETime.ToString("HH:mm:ss");
                control.SpeakLang.Text = $"{value.TranslationResult.From} -> {value.TranslationResult.To}";
                control.OriginalTextBlock.Text = value.TranslationResult.Query;
                control.TranslatedTextBlock.Text = value.TranslationResult.Result;
            }
            else
            {
                
            }
        }

        private void ShowFromButton_Click(object sender, RoutedEventArgs e)
        {
            OriginalArea.Visibility = OriginalArea.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
