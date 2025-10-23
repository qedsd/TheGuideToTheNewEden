using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Interfaces;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ViewLogPage : Page, IPage
    {
        private Window _window;
        public ViewLogPage()
        {
            this.InitializeComponent();
            Loaded += ViewLogPage_Loaded;
            ReadFile();
        }

        private void ViewLogPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ViewLogPage_Loaded;
            _window = Helpers.WindowHelper.GetWindowForElement(this);
            LogTextScrollViewer.LayoutUpdated += LogTextScrollViewer_LayoutUpdated;
        }

        private bool isAdded;
        private void LogTextScrollViewer_LayoutUpdated(object sender, object e)
        {
            if (isAdded)
            {
                isAdded = false;
                LogTextScrollViewer.ScrollToVerticalOffset(LogTextScrollViewer.ScrollableHeight);
            }
        }

        private async void ReadFile()
        {
            string file = Core.Log.GetLogFile();
            if (!string.IsNullOrEmpty(file) && File.Exists(file))
            {
                var logs = await File.ReadAllLinesAsync(file);
                if (logs.Length > 0)
                {
                    foreach (var log in logs)
                    {
                        AddLog(log);
                    }
                    isAdded = true;
                    //LogTextScrollViewer.ScrollToVerticalOffset(LogTextScrollViewer.ScrollableHeight);
                }
            }
            Core.Log.OnError += Log_OnError;
            Core.Log.OnWarn += Log_OnWarn;
            Core.Log.OnInfo += Log_OnInfo;
            Core.Log.OnDebug += Log_OnDebug;
        }

        private void Log_OnDebug(object msg)
        {
            AddLog("Debug", msg);
        }

        private void Log_OnInfo(object msg)
        {
            AddLog("Info", msg);
        }

        private void Log_OnWarn(object msg)
        {
            AddLog("Warn", msg);
        }

        private void Log_OnError(object msg)
        {
            AddLog("Error", msg);
        }
        private void AddLog(string type, object log)
        {
            _window.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                AddLog($"{DateTime.Now} {type} {log}");
                isAdded = true;
            });
        }
        private void AddLog(string log)
        {
            Paragraph paragraph = new Paragraph()
            {
                Margin = new Thickness(0, 8, 0, 8),
            };

            Run run = new Run()
            {
                Text = log
            };
            paragraph.Inlines.Add(run);
            LogTextBlock.Blocks.Add(paragraph);
        }

        public void Close()
        {
            Core.Log.OnError -= Log_OnError;
            Core.Log.OnWarn -= Log_OnWarn;
            Core.Log.OnInfo -= Log_OnInfo;
            Core.Log.OnDebug -= Log_OnDebug;
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("explorer.exe", Core.Log.GetLogFile());
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBlock.Blocks.Clear();
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
