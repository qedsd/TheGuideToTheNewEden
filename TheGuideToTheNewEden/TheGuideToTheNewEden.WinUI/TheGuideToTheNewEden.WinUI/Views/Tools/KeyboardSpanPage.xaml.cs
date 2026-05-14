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
using System.Threading;
using TheGuideToTheNewEden.WinUI.Extensions;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using TheGuideToTheNewEden.Core.Extensions;
using System.Data.OscarClient;


namespace TheGuideToTheNewEden.WinUI.Views.Tools
{
    public sealed partial class KeyboardSpanPage : Page, IPage, ITool
    {
        private ToolWindow _window;
        private SolidColorBrush _red;
        private SolidColorBrush _green;
        private SolidColorBrush _defaultColor;
        private string _windowTitle;
        public KeyboardSpanPage()
        {
            this.InitializeComponent();
            Loaded += KeyboardSpanPage_Loaded;
        }

        private void KeyboardSpanPage_Loaded(object sender, RoutedEventArgs e)
        {
            _window = this.GetWindow() as ToolWindow;
            _windowTitle = _window.GetDisplayTitle();
            Loaded -= KeyboardSpanPage_Loaded;
            ModeComboBox.SelectionChanged += ComboBox_SelectionChanged;
            _red = Helpers.ResourcesHelper.Get("DefaultRed") as SolidColorBrush;
            _green = Helpers.ResourcesHelper.Get("DefaultGreen") as SolidColorBrush;
            _defaultColor = Helpers.ResourcesHelper.Get("DefaultForegroundBrush") as SolidColorBrush;
            _=UpdateCharacters();
        }

        public void Close()
        {
            Stop();
        }

        public void GetWindowSize(out int width, out int height)
        {
            width = 400;
            height = 280;
        }

        public void NavigatedTo(object parameter)
        {
            
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(KeyboardTextBox.Text))
            {
                Start(KeyboardTextBox.Text, ModeComboBox.SelectedIndex, (int)CountNumberBox.Value, SpanNumberBox.Value);
            }
            RunningGrid.Visibility = Visibility.Visible;
            SettingGrid.Visibility = Visibility.Collapsed;
            PassCountTextBlock.Foreground = _defaultColor;
        }
        private DispatcherTimer _uiTimer;
        private DateTime _startTime;
        private bool _running = false;
        private void Start(string keyboard, int mode, int targetCount, double span)
        {
            _keyboardName = keyboard;
            _passCount = 0;
            _targetCount = targetCount;
            _span = span;
            Services.KeyboardService.Start();
            TargetCountTextBlock.Text = targetCount.ToString();
            TotalKeyboardCountTextBlock.Text = "0";
            TotalDuratioTextBlock.Text = "00:00:00";
            if (mode == 0)
            {
                Services.KeyboardService.OnKeyboardClicked += KeyboardService_OnKeyboardClicked1;
            }
            else
            {
                Services.KeyboardService.OnKeyboardClicked += KeyboardService_OnKeyboardClicked2;
            }
            if(_uiTimer == null)
            {
                _uiTimer = new DispatcherTimer()
                {
                    Interval = TimeSpan.FromSeconds(1),
                };
                _uiTimer.Tick += UITimer_Tick;
            }
            _startTime = DateTime.Now;
            _uiTimer.Start();
            _window.SetDisplayTitle(keyboard);
            _running = true;
        }

        private void UITimer_Tick(object sender, object e)
        {
            var duration = (DateTime.Now - _startTime);
            TotalDuratioTextBlock.Text = duration.ToString(@"hh\:mm\:ss");
        }
        private string _keyboardName;
        private double _span = 0;
        private int _passCount = 0;
        private int _targetCount = 0;
        private int _totalKeyboardCount = 0;
        private DispatcherTimer _calTimer;
        private bool _counting = false;
        /// <summary>
        /// 按时间间隔自动计算
        /// </summary>
        /// <param name="keys"></param>
        private void KeyboardService_OnKeyboardClicked1(List<Common.KeyboardHook.KeyboardInfo> keys)
        {
            if(_running && keys.FirstOrDefault(p=>p.Name == _keyboardName) != null)
            {
                _totalKeyboardCount++;
                TotalKeyboardCountTextBlock.Text = _totalKeyboardCount.ToString();
                if (!_counting)
                {
                    _passCount = 0;
                    if (_calTimer == null)
                    {
                        _calTimer = new DispatcherTimer();
                        _calTimer.Tick += CalTimer_Tick;
                    }
                    _calTimer.Interval = TimeSpan.FromSeconds(_span);
                    _calTimer.Start();
                    PassCountTextBlock.Text = "0";
                    PassCountTextBlock.Foreground = _green;
                    _counting = true;
                }
                else
                {
                    _calTimer.Stop();
                    _counting = false;
                    PassCountTextBlock.Foreground = _defaultColor;
                }
            }
            
        }

        private void CalTimer_Tick(object sender, object e)
        {
            _passCount++;
            PassCountTextBlock.Text = _passCount.ToString();
            if (_passCount == _targetCount)
            {
                PassCountTextBlock.Foreground = _red;
            }
        }
        /// <summary>
        /// 按按键次数
        /// </summary>
        /// <param name="keys"></param>
        private void KeyboardService_OnKeyboardClicked2(List<Common.KeyboardHook.KeyboardInfo> keys)
        {
            if (_running && keys.FirstOrDefault(p => p.Name == _keyboardName) != null)
            {
                _totalKeyboardCount++;
                TotalKeyboardCountTextBlock.Text = _totalKeyboardCount.ToString();
                _passCount++;
                if (_passCount > _targetCount)
                {
                    _passCount = 1;
                }
                PassCountTextBlock.Text = _passCount.ToString();
                PassCountTextBlock.Foreground = _green;
            }
        }

        private void Stop()
        {
            _uiTimer?.Stop();
            _calTimer?.Stop();
            Services.KeyboardService.Stop();
            Services.KeyboardService.OnKeyboardClicked -= KeyboardService_OnKeyboardClicked1;
            Services.KeyboardService.OnKeyboardClicked -= KeyboardService_OnKeyboardClicked2;
            _window.SetDisplayTitle(_windowTitle);
            _running = false;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            if((sender as ComboBox).SelectedIndex == 0)
            {
                SpanNumberBox.Visibility = Visibility.Visible;
            }
            else
            {
                SpanNumberBox.Visibility = Visibility.Collapsed;
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            RunningGrid.Visibility = Visibility.Collapsed;
            SettingGrid.Visibility = Visibility.Visible;
            Stop();
        }

        private void DataButton_Click(object sender, RoutedEventArgs e)
        {
            string character = CharacterComboBox.SelectedItem as string;
            if (!string.IsNullOrEmpty(character))
            {
                var hwnd = Helpers.WindowHelper.GetGameHwndByCharacterName(character);
                if (hwnd != IntPtr.Zero)
                {
                    Helpers.WindowHelper.SetForegroundWindow_Click(hwnd);
                }
            }
        }

        private void UpdateCharacters_Click(object sender, RoutedEventArgs e)
        {
            _=UpdateCharacters();
        }

        private const string _processName = "exefile";
        private async Task UpdateCharacters()
        {
            List<string> targetProcess = new List<string>();
            await Task.Run(() =>
            {
                var allProcesses = System.Diagnostics.Process.GetProcesses();
                if (allProcesses.NotNullOrEmpty())
                {
                    foreach (var process in allProcesses)
                    {
                        if (process.ProcessName.Contains(_processName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (process.MainWindowHandle != IntPtr.Zero)
                            {
                                targetProcess.Add(process.MainWindowTitle);
                            }
                        }
                    }
                }
            });
            CharacterComboBox.ItemsSource = targetProcess;
        }
    }
}
