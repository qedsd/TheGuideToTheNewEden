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
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.ViewModels;
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

        public static readonly DependencyProperty ProcessesSourceProperty
           = DependencyProperty.Register(
               nameof(ProcessesSource),
               typeof(IEnumerable<ProcessInfo>),
               typeof(GamePreviewGroupHotKeyControl),
               new PropertyMetadata(default(IEnumerable<ProcessInfo>)));

        public IEnumerable<ProcessInfo> ProcessesSource
        {
            get => (IEnumerable<ProcessInfo>)GetValue(ProcessesSourceProperty);
            set => SetValue(ProcessesSourceProperty, value);
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
            CheckBox_ActiveRestart.IsChecked = HotKeyGroup?.Restart;
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
            HotKeyGroup.Restart = CheckBox_ActiveRestart.IsChecked == true;
            SaveCommand?.Execute(null);
            SaveClicked?.Invoke(this,HotKeyGroup);
        }

        private void SelectFromProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (HotKeyGroup == null) return;

            var processes = ProcessesSource;
            if (processes == null)
            {
                // Fallback: walk up the visual tree to find the parent GamePreviewMgrViewModel
                processes = FindProcessesFromVisualTree();
            }
            if (processes == null) return;

            // 收集已在编辑框中的名称（未确认保存），去重过滤
            var existingInEditBox = new HashSet<string>();
            if (!string.IsNullOrEmpty(EditingBox.Text))
            {
                foreach (var n in EditingBox.Text.Split(','))
                {
                    var trimmed = n.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        existingInEditBox.Add(trimmed);
                }
            }

            var availableNames = new List<string>();
            foreach (var process in processes)
            {
                var name = process.GetCharacterName();
                if (!string.IsNullOrEmpty(name)
                    && !HotKeyGroup.GameNames.Contains(name)
                    && !existingInEditBox.Contains(name))
                {
                    availableNames.Add(name);
                }
            }

            if (availableNames.Count == 0) return;

            var flyout = new Flyout();
            var stackPanel = new StackPanel();

            var headerText = new TextBlock
            {
                Text = Helpers.ResourcesHelper.GetString("GamePreviewMgrPage_GroupHotkey_SelectFromProcess"),
                Margin = new Thickness(0, 0, 0, 8),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            };

            var listView = new ListView
            {
                SelectionMode = ListViewSelectionMode.Multiple,
                Height = 280,
                MinWidth = 250,
                ItemsSource = availableNames,
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0),
            };

            var addButton = new Button
            {
                Content = Helpers.ResourcesHelper.GetString("General_Add"),
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            };
            addButton.Click += (s, args) =>
            {
                var selectedNames = listView.SelectedItems.Cast<string>().ToList();
                if (selectedNames.Any())
                {
                    var existing = EditingBox.Text;
                    if (!string.IsNullOrEmpty(existing))
                    {
                        EditingBox.Text = existing + "," + string.Join(",", selectedNames);
                    }
                    else
                    {
                        EditingBox.Text = string.Join(",", selectedNames);
                    }
                }
                flyout.Hide();
            };

            var cancelButton = new Button
            {
                Content = Helpers.ResourcesHelper.GetString("General_Cancel"),
                Margin = new Thickness(8, 0, 0, 0),
            };
            cancelButton.Click += (s, args) => flyout.Hide();

            buttonPanel.Children.Add(addButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(headerText);
            stackPanel.Children.Add(listView);
            stackPanel.Children.Add(buttonPanel);
            flyout.Content = stackPanel;
            flyout.ShowAt(sender as Button);
        }

        private IEnumerable<ProcessInfo> FindProcessesFromVisualTree()
        {
            DependencyObject current = this;
            while (current != null)
            {
                current = VisualTreeHelper.GetParent(current);
                if (current is FrameworkElement fe && fe.DataContext is GamePreviewMgrViewModel vm)
                {
                    return vm.Processes;
                }
            }
            return null;
        }
    }
}
