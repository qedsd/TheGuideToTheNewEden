using ESI.NET.Models.PlanetaryInteraction;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class GaemLogMsgWindow
    {
        public object Tag { get; set; }
        private readonly BaseWindow _window = new BaseWindow();
        private RichTextBlock _mainContent;
        private ScrollViewer _scrollViewer;
        public delegate void HideDelegate(GaemLogMsgWindow messageWindow);
        public event HideDelegate OnHided;
        public delegate void ShowGameButtonDelegate(GaemLogMsgWindow gaemLogMsgWindow);
        public event ShowGameButtonDelegate OnShowGameButtonClick;
        public int ListenerID { get; private set; }
        public string ListenerName { get; private set; }
        public GaemLogMsgWindow(int listenerID, string listenerName)
        {
            ListenerID = listenerID;
            ListenerName = listenerName;
            _mainContent = new RichTextBlock()
            {
                Margin = new Microsoft.UI.Xaml.Thickness(10)
            };
            Grid grid = new Grid();
            _scrollViewer = new ScrollViewer()
            {
                Margin = new Thickness(0,0,0,32)
            };
            _scrollViewer.Content = _mainContent;
            _scrollViewer.LayoutUpdated += ScrollViewer_LayoutUpdated;
            grid.Children.Add(_scrollViewer);
            Button button = new Button()
            {
                Content = Helpers.ResourcesHelper.GetString("GameLogMonitorPage_ShowGame"),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            button.Click += Button_Click;
            grid.Children.Add(button);

            _window.HideAppDisplayName();
            _window.SetSmallTitleBar();
            _window.AppWindow.Closing += AppWindow_Closing;
            _window.MainContent = grid;
            _window.SetWindowSize(400, 300);
            _window.SetIsAlwaysOnTop(true);
            Helpers.WindowHelper.CenterToScreen(_window);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OnShowGameButtonClick?.Invoke(this);
        }

        private bool _isAdded = false;
        private void ScrollViewer_LayoutUpdated(object sender, object e)
        {
            if (_isAdded)
            {
                _isAdded = false;
                _scrollViewer.ScrollToVerticalOffset(_scrollViewer.ScrollableHeight);
            }
        }


        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            args.Cancel = true;
            Hide();
        }

        public void SetTitle(string text)
        {
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                _window.SetHeadText(text);
            });
        }
        public void Show(string content)
        {
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                Paragraph paragraph = new Paragraph()
                {
                    Margin = new Thickness(0, 16, 0, 16),
                };
                Run contentRun = new Run()
                {
                    FontWeight = FontWeights.Bold,
                    Text = content
                };
                paragraph.Inlines.Add(contentRun);
                var lastParagraph = _mainContent.Blocks.LastOrDefault() as Paragraph;
                if (lastParagraph != null)
                {
                    foreach (var run in lastParagraph.Inlines)
                    {
                        run.FontWeight = FontWeights.Normal;
                    }
                }
                _mainContent.Blocks.Add(paragraph);
                _isAdded = true;
                _window.Activate();
            });
        }
        public void Show(string title, string content)
        {
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                SetTitle(title);
                Show(content);
            });
        }
        public void Clear()
        {
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                _mainContent.Blocks.Clear();
            });
        }
        public void Hide()
        {
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                _window.Hide();
                OnHided?.Invoke(this);
            });
        }
        public void Close()
        {
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                _window.AppWindow.Closing -= AppWindow_Closing;
                _window.Close();
            });
        }
    }
}
