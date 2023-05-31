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
using ESI.NET.Models.Character;
using ESI.NET.Models.SSO;
using System.Collections.ObjectModel;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class SelecteCharacterControl : UserControl
    {
        public ObservableCollection<AuthorizedCharacterData> Characters { get; set; }
        public SelecteCharacterControl()
        {
            this.InitializeComponent();
            Characters = Services.CharacterService.CharacterOauths;
            CharacterComboBox.ItemsSource = Characters;
        }
        #region SelectedIndex
        public static readonly DependencyProperty SelectedIndexProperty
            = DependencyProperty.Register(
                nameof(SelectedIndex),
                typeof(int),
                typeof(SelecteCharacterControl),
                new PropertyMetadata(-1, new PropertyChangedCallback(SelectedIndexPropertyChanged)));

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }
        private static void SelectedIndexPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as SelecteCharacterControl).CharacterComboBox.SelectedIndex = (int)e.NewValue;
            (d as SelecteCharacterControl).OnSelectedItemChanged?.Invoke((d as SelecteCharacterControl).CharacterComboBox.SelectedItem as AuthorizedCharacterData);
        }
        #endregion
        #region SelectedItem
        public static readonly DependencyProperty SelectedItemProperty
            = DependencyProperty.Register(
                nameof(SelectedItem),
                typeof(AuthorizedCharacterData),
                typeof(SelecteCharacterControl),
                new PropertyMetadata(null, new PropertyChangedCallback(SelectedItemPropertyChanged)));

        public AuthorizedCharacterData SelectedItem
        {
            get => (AuthorizedCharacterData)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }
        private static void SelectedItemPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as SelecteCharacterControl).CharacterComboBox.SelectedItem = (AuthorizedCharacterData)e.NewValue;
        }
        #endregion
        public delegate void SelectedItemChangedEventHandel(AuthorizedCharacterData selectedItem);
        private SelectedItemChangedEventHandel OnSelectedItemChanged;

        private void CharacterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedItem = CharacterComboBox.SelectedItem as AuthorizedCharacterData;
        }
    }
}
