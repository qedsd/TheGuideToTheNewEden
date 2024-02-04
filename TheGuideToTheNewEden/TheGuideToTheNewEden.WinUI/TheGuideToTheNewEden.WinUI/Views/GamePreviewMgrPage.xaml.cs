using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WinUI.Common;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.ViewModels;
using TheGuideToTheNewEden.WinUI.Wins;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class GamePreviewMgrPage : Page,IPage
    {
        private BaseWindow Window;
        private Microsoft.UI.Windowing.AppWindow AppWindow;
        public GamePreviewMgrPage()
        {
            this.InitializeComponent();
            Loaded += GamePreviewMgrPage_Loaded2;
            Loaded += GamePreviewMgrPage_Loaded;
            Unloaded += GamePreviewMgrPage_Unloaded;
        }

        private void GamePreviewMgrPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (lastThumb != IntPtr.Zero)
            {
                WindowCaptureHelper.HideThumb(lastThumb);
            }
        }

        private IntPtr windowHandle = IntPtr.Zero;
        private void GamePreviewMgrPage_Loaded2(object sender, RoutedEventArgs e)
        {
            Window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
            VM.Window = Window;
            windowHandle = Helpers.WindowHelper.GetWindowHandle(Window);
            if (AppWindow != null)
            {
                AppWindow.Closing -= AppWindow_Closing;
            }
            AppWindow = Helpers.WindowHelper.GetAppWindow(Window);
            AppWindow.Closing += AppWindow_Closing;
            if(lastThumb!= IntPtr.Zero && ProcessList.SelectedItem != null)
            {
                lastThumb = WindowCaptureHelper.Show(windowHandle, (ProcessList.SelectedItem as ProcessInfo).MainWindowHandle);
            }
            UpdateThumbDestination();
        }
        private void GamePreviewMgrPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= GamePreviewMgrPage_Loaded;
            ProcessList.SelectionChanged += ProcessList_SelectionChanged;
            PreviewGrid.SizeChanged += PreviewGrid_SizeChanged;
        }

        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            VM.Dispose();
        }

        private void PreviewGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateThumbDestination();
        }

        private IntPtr lastThumb = IntPtr.Zero;
        private void ProcessList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lastThumb != IntPtr.Zero)
            {
                WindowCaptureHelper.HideThumb(lastThumb);
            }
            if (e.AddedItems.Count > 0)
            {
                var process = e.AddedItems.FirstOrDefault() as Core.Models.GamePreviews.ProcessInfo;
                if (process != null)
                {
                    lastThumb = WindowCaptureHelper.Show(windowHandle, process.MainWindowHandle);
                    UpdateThumbDestination();
                }
            }
        }
        private void UpdateThumbDestination()
        {
            var thumbSize = WindowCaptureHelper.GetThumbSourceSize(lastThumb);
            if(thumbSize.x > thumbSize.y)//横向
            {
                var showWPresent = PreviewGrid.ActualWidth / ContentGrid.ActualWidth;
                var showW = showWPresent * (AppWindow.ClientSize.Width - (ContentGrid.Margin.Left + ContentGrid.Margin.Right));
                var showH = showW / thumbSize.x * thumbSize.y;
                int left = (int)(AppWindow.ClientSize.Width * (1 - showWPresent));
                int top = (int)(AppWindow.ClientSize.Height / 2 - showH / 2);
                int right = (int)(left + showW);
                int bottom = (int)(top + showH);
                WindowCaptureHelper.UpdateThumbDestination(lastThumb, new WindowCaptureHelper.Rect(left, top, right, bottom));
            }
            else//竖向
            {
                var showHPresent = PreviewGrid.ActualHeight / Window.Content.ActualSize.Y;//不能显示到标题栏
                var showH = showHPresent * (AppWindow.ClientSize.Height - (ContentGrid.Margin.Top + ContentGrid.Margin.Bottom));
                var showW = showH / thumbSize.y * thumbSize.x;
                var showWPresent = PreviewGrid.ActualWidth / ContentGrid.ActualWidth;
                int top = (int)(AppWindow.ClientSize.Height * (1 - showHPresent));
                int left = (int)(AppWindow.ClientSize.Width * (1 - showWPresent * 0.5) - showW / 2);
                int right = (int)(left + showW);
                int bottom = (int)(top + showH);

                //限制范围内
                var minLeft = (1 - (PreviewGrid.ActualWidth / ContentGrid.ActualWidth)) * AppWindow.ClientSize.Width;
                var maxRight = AppWindow.ClientSize.Width;
                var maxBottom = AppWindow.ClientSize.Height;
                left = left < minLeft ? (int)minLeft : left;
                right = right > maxRight ? maxRight : right;
                bottom = bottom > maxBottom ? maxBottom : bottom;
                WindowCaptureHelper.UpdateThumbDestination(lastThumb, new WindowCaptureHelper.Rect(left, top, right, bottom));
            }
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            VM.Setting.Name = SettingNameBox.Text;//只在开始后才保存修改的名字
            VM.StartCommand.Execute(null);
        }

        private void RemoveSetting_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                VM.RemoveSettingCommand.Execute(button.DataContext as PreviewItem);
            }
        }

        private void SettingList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SettingListFlyout.Hide();
        }

        private void ProcessList_DragEnter(object sender, DragEventArgs e)
        {
            e.DragUIOverride.IsGlyphVisible = false;
        }

        private void ProcessList_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            var item = e.Items.First() as Core.Models.GamePreviews.ProcessInfo;
            e.Data.SetText(item.GUID);
            e.Data.RequestedOperation = DataPackageOperation.Move;
        }

        private void ProcessList_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Move;
        }

        private async void ProcessList_Drop(object sender, DragEventArgs e)
        {
            ListView target = (ListView)sender;
            if (e.DataView.Contains(StandardDataFormats.Text))
            {
                DragOperationDeferral def = e.GetDeferral();
                string guid = await e.DataView.GetTextAsync();
                var process = VM.Processes.FirstOrDefault(p => p.GUID == guid);
                if (process == null)
                {
                    return;
                }
                Windows.Foundation.Point pos = e.GetPosition(target.ItemsPanelRoot);
                int index = 0;
                if (target.Items.Count != 0)
                {
                    // Get a reference to the first item in the ListView
                    ListViewItem sampleItem = (ListViewItem)target.ContainerFromIndex(0);

                    // Adjust itemHeight for margins
                    double itemHeight = sampleItem.ActualHeight + sampleItem.Margin.Top + sampleItem.Margin.Bottom;

                    // Find index based on dividing number of items by height of each item
                    index = Math.Min(target.Items.Count - 1, (int)(pos.Y / itemHeight));

                    // Find the item being dropped on top of.
                    ListViewItem targetItem = (ListViewItem)target.ContainerFromIndex(index);

                    // If the drop position is more than half-way down the item being dropped on
                    //      top of, increment the insertion index so the dropped item is inserted
                    //      below instead of above the item being dropped on top of.
                    Windows.Foundation.Point positionInItem = e.GetPosition(targetItem);
                    if (positionInItem.Y > itemHeight / 2)
                    {
                        index++;
                    }

                    // Don't go out of bounds
                    index = Math.Min(target.Items.Count, index);
                }
                VM.Processes.Remove(process);
                VM.Processes.Insert(index, process);
            }
        }

        public void Close()
        {
            VM.Dispose();
        }

        private void MenuFlyoutItem_Up_Click(object sender, RoutedEventArgs e)
        {
            var info = (sender as FrameworkElement).DataContext as ProcessInfo;
            if(info != null)
            {
                int index = VM.Processes.IndexOf(info);
                if(index > 0)
                {
                    VM.Processes.Move(index, index - 1);
                }
            }
        }

        private void MenuFlyoutItem_Down_Click(object sender, RoutedEventArgs e)
        {
            var info = (sender as FrameworkElement).DataContext as ProcessInfo;
            if (info != null)
            {
                int index = VM.Processes.IndexOf(info);
                if (index < VM.Processes.Count - 2)
                {
                    VM.Processes.Move(index, index + 1);
                }
            }
        }
    }
}
