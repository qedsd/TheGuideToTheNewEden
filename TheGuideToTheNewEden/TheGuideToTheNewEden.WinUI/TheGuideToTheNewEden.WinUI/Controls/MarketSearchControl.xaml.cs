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

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class MarketSearchControl : UserControl
    {
        private List<InvType> _types;
        public MarketSearchControl()
        {
            this.InitializeComponent();
            Init();
        }
        private async void Init()
        {
            _types = await Core.Services.DB.InvTypeService.QueryMarketTypesAsync();
        }

        public static readonly DependencyProperty SelectedInvTypeProperty
            = DependencyProperty.Register(
                nameof(SelectedInvType),
                typeof(InvType),
                typeof(MarketSearchControl),
                new PropertyMetadata(null, new PropertyChangedCallback(SelectedInvTypePropertyChanged)));

        public InvType SelectedInvType
        {
            get => (InvType)GetValue(SelectedInvTypeProperty);
            set => SetValue(SelectedInvTypeProperty, value);
        }
        private static void SelectedInvTypePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as MarketSearchControl).OnSelectedInvTypeChanged?.Invoke(e.NewValue as InvType);
        }

        public delegate void SelectedInvTypeChangedEventHandel(InvType selectedInvType);
        private SelectedInvTypeChangedEventHandel OnSelectedInvTypeChanged;


        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            SelectedInvType = args.SelectedItem as InvType;
            Search_AutoSuggestBox.Text = SelectedInvType.TypeName;
        }

        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (SelectedInvType?.TypeName == sender.Text)
            {
                return;
            }
            if (string.IsNullOrEmpty(sender.Text))
            {
                sender.ItemsSource = null;
            }
            else
            {
                sender.ItemsSource = _types.Where(p => p.TypeName.Contains(sender.Text, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }
    }
}
