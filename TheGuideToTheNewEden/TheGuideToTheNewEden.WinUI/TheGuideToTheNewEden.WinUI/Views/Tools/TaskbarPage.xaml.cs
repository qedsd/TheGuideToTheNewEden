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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TheGuideToTheNewEden.WinUI.Views.Tools
{
    public sealed partial class TaskbarPage : Page, IPage, ITool
    {
        public TaskbarPage()
        {
            this.InitializeComponent();
        }

        public void Close()
        {
        }

        public void GetWindowSize(out int width, out int height)
        {
            width = 200;
            height = 140;
        }

        public void NavigatedTo(object parameter)
        {
            
        }
        private List<IntPtr> GetTargetWindows()
        {
            var windows = new List<IntPtr>();
            foreach (var process in System.Diagnostics.Process.GetProcessesByName("exefile"))
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    windows.Add(process.MainWindowHandle);
                }
            }
            return windows;
        }

        private void Separate_Click(object sender, RoutedEventArgs e)
        {
            var windows = GetTargetWindows();
            foreach (var window in windows)
            {
                Helpers.TaskbarHelper.SetAppUserModelIdForWindow(window, Guid.NewGuid().ToString());
            }
        }

        private void Merge_Click(object sender, RoutedEventArgs e)
        {
            string id = Guid.NewGuid().ToString();
            var windows = GetTargetWindows();
            foreach (var window in windows)
            {
                Helpers.TaskbarHelper.SetAppUserModelIdForWindow(window, id);
            }
        }
    }
}
