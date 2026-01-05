using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class GamePreviewGroupHotKeyControl : UserControl
    {
        public GamePreviewGroupHotKeyControl()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty HotKeyGroupProperty
           = DependencyProperty.Register(
               nameof(HotKeyGroup),
               typeof(object),
               typeof(GamePreviewGroupHotKeyControl),
               new PropertyMetadata(default, new PropertyChangedCallback(HotKeyGroupPropertyChanged)));

        public PreviewHotKeyGroup HotKeyGroup
        {
            get => (PreviewHotKeyGroup)GetValue(HotKeyGroupProperty);
            set => SetValue(HotKeyGroupProperty, value);
        }
        private static void HotKeyGroupPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as GamePreviewGroupHotKeyControl).UpdateGroupHotKey();
        }

        public static readonly DependencyProperty SaveCommandProperty
           = DependencyProperty.Register(
               nameof(SaveCommand),
               typeof(ICommand),
               typeof(GamePreviewGroupHotKeyControl),
               new PropertyMetadata(default(ICommand)));

        public ICommand SaveCommand
        {
            get => (ICommand)GetValue(SaveCommandProperty);
            set => SetValue(SaveCommandProperty, value);
        }

        private EventHandler<PreviewHotKeyGroup> SaveClicked;
        public event EventHandler<PreviewHotKeyGroup> OnSaveClicked
        {
            add
            {
                SaveClicked += value;
            }
            remove
            {
                SaveClicked -= value;
            }
        }

        private void UpdateGroupHotKey()
        {
            if (HotKeyGroup != null && HotKeyGroup.GameNames.Count > 0)
            {
                GameNameListBox.ItemsSource = HotKeyGroup.GameNames.ToList();
            }
            else
            {
                GameNameListBox.ItemsSource = null;
            }
            NameTextBox.Text = HotKeyGroup?.GroupName;
            ForwardTextBox.Text = HotKeyGroup?.SwitchHotkey_Forward;
            BackwardTextBox.Text = HotKeyGroup?.SwitchHotkey_Backward;
        }
        private void StartEidt()
        {
            GameNameListBox.Visibility = Visibility.Collapsed;
            NormalPanel.Visibility = Visibility.Collapsed;
            EditingGrid.Visibility = Visibility.Visible;
            EditingPanel.Visibility = Visibility.Visible;
            EditingBox.Text = string.Join(',', HotKeyGroup.GameNames);
        }
        private void ExitEidt()
        {
            GameNameListBox.Visibility = Visibility.Visible;
            NormalPanel.Visibility = Visibility.Visible;
            EditingGrid.Visibility = Visibility.Collapsed;
            EditingPanel.Visibility = Visibility.Collapsed;
        }
        private void EditGameName_Click(object sender, RoutedEventArgs e)
        {
            StartEidt();
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            ExitEidt();
        }

        private void ConfirmEdit_Click(object sender, RoutedEventArgs e)
        {
            ExitEidt();
            HotKeyGroup.GameNames.Clear();
            HotKeyGroup.GroupName = NameTextBox.Text;
            if (!string.IsNullOrEmpty(EditingBox.Text))
            {
                var array = EditingBox.Text.Split(',');
                if (array != null && array.Length > 0)
                {
                    foreach (var name in array)
                    {
                        if(!string.IsNullOrEmpty(name))
                        {
                            HotKeyGroup.GameNames.Add(name);
                        }
                    }
                }
            }
            GameNameListBox.ItemsSource = HotKeyGroup.GameNames.ToList();
        }

        private void SaveEdit_Click(object sender, RoutedEventArgs e)
        {
            HotKeyGroup.SwitchHotkey_Forward = ForwardTextBox.Text;
            HotKeyGroup.SwitchHotkey_Backward = BackwardTextBox.Text;
            SaveCommand?.Execute(null);
            SaveClicked?.Invoke(this,HotKeyGroup);
        }

        private void ApplyForward_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ApplyBackward_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
