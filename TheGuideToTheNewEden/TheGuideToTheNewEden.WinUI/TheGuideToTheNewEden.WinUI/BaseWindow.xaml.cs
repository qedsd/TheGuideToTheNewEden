using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using TheGuideToTheNewEden.WinUI.Views;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT;

namespace TheGuideToTheNewEden.WinUI
{
    public partial class BaseWindow : Window
    {
        public WinUICommunity.IThemeService ThemeService { get; set; }
        public BaseWindow()
        {
            Init(true, true, false);
        }
        public BaseWindow(bool useThemeService)
        {
            Init(useThemeService, true, false);
        }
        public BaseWindow(bool useThemeService, bool useBackgroun)
        {
            Init(useThemeService, useBackgroun, false);
        }
        public BaseWindow(bool useThemeService, bool useBackgroun, bool hideCaptionButton)
        {
            Init(useThemeService, useBackgroun, hideCaptionButton);
        }
        public void Init(bool useThemeService, bool useBackground, bool hideCaptionButton)
        {
            this.InitializeComponent();
            if(useThemeService)
            {
                ThemeService = new WinUICommunity.ThemeService();
                ThemeService.Initialize(this, false);
                ThemeService.ConfigElementTheme(ThemeSelectorService.Theme);
                ThemeService.ConfigBackdrop();
                ThemeSelectorService.OnChangedTheme += ThemeSelectorService_OnChangedTheme;
                ThemeSelectorService_OnChangedTheme(ThemeSelectorService.Theme);
                BackdropSelectorService.OnBackdropTypeChanged += BackdropSelectorService_OnBackdropTypeChanged;
                BackdropSelectorService.OnCustomPictureFileChanged += BackdropSelectorService_OnCustomPictureFileChanged;
                BackdropSelectorService.OnCustomPictureOverlapColorChanged += BackdropSelectorService_OnCustomPictureOverlapColorChanged;
            }
            TitleBarHeight = (int)(WindowHelper.GetTitleBarHeight(WindowHelper.GetWindowHandle(this)) / Helpers.WindowHelper.GetDpiScale(this));//只能在ExtendsContentIntoTitleBar前获取，之后会变为0
            this.Title = Helpers.ResourcesHelper.GetString("AppDisplayName");
            Helpers.WindowHelper.TrackWindow(this);
            
            Helpers.WindowHelper.CenterToScreen(this);
            WindowHelper.GetAppWindow(this).SetIcon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo_64.ico"));
            if (useBackground && BackdropSelectorService.BackdropTypeValue == BackdropSelectorService.BackdropType.CustomPicture)
            {
                if(!string.IsNullOrEmpty(BackdropSelectorService.CustomPictureFileValue))
                {
                    LoadCustomPicture();
                }
            }
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            if (hideCaptionButton)
            {
                var presenter = Helpers.WindowHelper.GetOverlappedPresenter(this);
                presenter.SetBorderAndTitleBar(true, false);
            }
            
            //var titleBar = WindowHelper.GetAppWindow(this).TitleBar;
            //titleBar.ForegroundColor = Colors.Green;
            //titleBar.BackgroundColor = Colors.Green;
            //titleBar.ButtonForegroundColor = Colors.Yellow;
            //titleBar.ButtonBackgroundColor = Colors.Transparent;
            //titleBar.ButtonHoverForegroundColor = Colors.Gainsboro;
            //titleBar.ButtonHoverBackgroundColor = Colors.DarkSeaGreen;
            //titleBar.ButtonPressedForegroundColor = Colors.Gray;
            //titleBar.ButtonPressedBackgroundColor = Colors.LightGreen;
            //titleBar.InactiveForegroundColor = Colors.Yellow;
            //titleBar.InactiveBackgroundColor = Colors.Yellow;
            //titleBar.ButtonInactiveForegroundColor = Colors.Yellow;
            //titleBar.ButtonInactiveBackgroundColor = Colors.Yellow;
        }
        public object MainContent
        {
            get => ContentFrame.Content;

            set => ContentFrame.Content = value;
        }
        public UIElement MainUIElement
        {
            get => MainWindowGrid;
        }

        /// <summary>
        /// 无缩放下的原始标题栏高度
        /// </summary>
        public int TitleBarHeight { get;private set; }
        public string Head
        {
            get => HeadTextBlock.Text;

            set
            {
                SetHeadText(value);
            }
        }
        public void SetHeadText(string head)
        {
            if (string.IsNullOrEmpty(head))
            {
                HeadPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                HeadPanel.Visibility = Visibility.Visible;
                HeadTextLine.Visibility = AppDisplayNameTextBlock.Visibility;
                HeadTextBlock.Text = head;
            }
        }
        public void HideAppDisplayName()
        {
            AppDisplayNameTextBlock.Visibility = Visibility.Collapsed;
            HeadTextLine.Visibility = Visibility.Collapsed;
        }
        public void ShowAppDisplayName()
        {
            AppDisplayNameTextBlock.Visibility = Visibility.Visible;
            HeadTextLine.Visibility = HeadPanel.Visibility;
        }
        public double GetTitleBarDesignHeight()
        {
            return AppTitleBar.Height;
        }
        public void SetLargeTitleBar()
        {
            SetTitleBarHeight(42);
            SetAppTitleLeftBorder(20);
        }
        public void SetSmallTitleBar()
        {
            SetTitleBarHeight(32);
            SetAppTitleLeftBorder(10);
        }
        public void SetTitleBarHeight(int h)
        {
            AppTitleBarGrid.Height = h;
            //AppTitleBar.Height = h;
        }
        public void SetAppTitleLeftBorder(int w)
        {
            AppTitleLeftBorder.Width = w;
        }
        public void SetAppTitleBarLeft(int w)
        {
            AppTitleBar.Margin = new Thickness(w, AppTitleBar.Margin.Top, AppTitleBar.Margin.Right, AppTitleBar.Margin.Bottom);
        }
        /// <summary>
        /// 隐藏标题，不隐藏logo
        /// </summary>
        public void HideAppTitle()
        {
            AppDisplayNameTextBlock.Visibility = Visibility.Collapsed;
            HeadPanel.Visibility = Visibility.Collapsed;
            InfoBar.Margin = new Thickness(0, 32, 0, 0);
        }
        public void HideNavButton()
        {
            var presenter = Helpers.WindowHelper.GetOverlappedPresenter(this);
            presenter.SetBorderAndTitleBar(true, false);
            TitleBarExtendsToNavButton();
        }
        /// <summary>
        /// 完全隐藏标题栏
        /// 包括logo、标题、最小最大关闭按钮
        /// </summary>
        public void HideHideAppHeader()
        {
            AppTitleContentArea.Visibility = Visibility.Collapsed;
            HideNavButton();
            MainContentExtendsToTitleBar();
        }
        /// <summary>
        /// 将TitleBar拖动区域扩展到原生导航键（最小最大关闭）位置
        /// </summary>
        public void TitleBarExtendsToNavButton()
        {
            AppTitleBarGrid.Margin = new Thickness(0);
        }
        /// <summary>
        /// 把MainContent扩展到标题栏上
        /// </summary>
        public void MainContentExtendsToTitleBar()
        {
            ContentArea.Margin = new Thickness(0, -32, 0, 0); ;
        }
        /// <summary>
        /// 主页模式
        /// 隐藏标题，显示内容区域上移到标题栏位置
        /// </summary>
        public void SetTavViewHomeMode()
        {
            ContentArea.SetValue(Grid.RowSpanProperty, 2);
            ContentArea.SetValue(Grid.RowProperty, 0);
            HideAppTitle();
        }

        private System.Timers.Timer Timer;
        private void StartTimer()
        {
            if (Timer == null)
            {
                Timer = new System.Timers.Timer(3000);
                Timer.AutoReset = false;
                Timer.Elapsed += Timer_Elapsed;
            }
            Timer.Start();
        }
        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Helpers.WindowHelper.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                InfoBar.Message = string.Empty;
                InfoBar.IsOpen = false;
            });
        }

        public void ShowMsg(string msg, bool autoClose = true)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                InfoBar.Severity = InfoBarSeverity.Informational;
                InfoBar.Message = msg;
                InfoBar.IsOpen = true;
                if (autoClose)
                {
                    StartTimer();
                }
            });
        }
        public void ShowError(string msg, bool autoClose = false)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                InfoBar.Severity = InfoBarSeverity.Error;
                InfoBar.Message = msg;
                InfoBar.IsOpen = true;
                if (autoClose)
                {
                    StartTimer();
                }
            });
        }
        public void ShowSuccess(string msg, bool autoClose = true)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                InfoBar.Severity = InfoBarSeverity.Success;
                InfoBar.Message = msg;
                InfoBar.IsOpen = true;
                if (autoClose)
                {
                    StartTimer();
                }
            });
        }
        public void ShowWaiting(string tip = null)
        {
            if (NavigationService.ShowWaiting(tip))
            {
                return;
            }
            ShowWindowWaiting(tip);
        }
        public void ShowWindowWaiting(string tip = null)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (string.IsNullOrEmpty(tip))
                {
                    WaitingText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    WaitingText.Visibility = Visibility.Visible;
                    WaitingText.Text = tip;
                }
                WaitingGrid.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                WaitingProgressRing.IsActive = true;
            });
        }
        public void ShowWaiting(TabViewBasePage page, string tip = null)
        {
            NavigationService.ShowWaiting(page, tip);
        }
        public void HideWaiting()
        {
            if (NavigationService.HideWaiting())
            {
                return;
            }
            HideWindowWaiting();
        }
        public void HideWaiting(TabViewBasePage page)
        {
            NavigationService.HideWaiting(page);
        }
        public void HideWindowWaiting()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                WaitingGrid.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                WaitingProgressRing.IsActive = false;
            });
        }
        public void Hide()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                WindowHelper.GetAppWindow(this)?.Hide();
            });
        }

        private void ThemeSelectorService_OnChangedTheme(ElementTheme theme)
        {
            if (BackdropSelectorService.BackdropTypeValue == BackdropSelectorService.BackdropType.None)
            {
                switch (theme)
                {
                    case ElementTheme.Dark: MainWindowGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32)); break;
                    case ElementTheme.Light: MainWindowGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 243, 243)); break;
                    case ElementTheme.Default:
                        {
                            if (Application.Current.RequestedTheme == ApplicationTheme.Dark)
                            {
                                MainWindowGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32));
                            }
                            else
                            {
                                MainWindowGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 243, 243));
                            }
                        }
                        break;
                }
            }
        }

        private void BackdropSelectorService_OnBackdropTypeChanged(object sender, BackdropSelectorService.BackdropType e)
        {
            switch(e)
            {
                case BackdropSelectorService.BackdropType.None: ThemeSelectorService_OnChangedTheme(ThemeSelectorService.Theme);break;
                case BackdropSelectorService.BackdropType.CustomPicture:
                    {
                        ThemeSelectorService_OnChangedTheme(ThemeSelectorService.Theme);
                        LoadCustomPicture();
                    }
                    break;
                default:
                    {
                        MainWindowGrid.Background = new SolidColorBrush(Colors.Transparent);
                        UnloadCustomPicture();
                    }
                    break;
            }
        }

        private void BackdropSelectorService_OnCustomPictureOverlapColorChanged(object sender, string e)
        {
            BackgroundBrush.Color = BackdropSelectorService.GetCustomPictureOverlapColor();
        }

        private void BackdropSelectorService_OnCustomPictureFileChanged(object sender, string e)
        {
            LoadCustomPicture();
        }

        private void LoadCustomPicture()
        {
            BackgroundGrid.Visibility = Visibility.Visible;
            BackgroundBrush.Color = BackdropSelectorService.GetCustomPictureOverlapColor();
            if (File.Exists(BackdropSelectorService.CustomPictureFileValue))
            {
                BackgroundImage.ImageSource = new BitmapImage(new Uri(BackdropSelectorService.CustomPictureFileValue));
            }
        }
        private void UnloadCustomPicture()
        {
            BackgroundGrid.Visibility = Visibility.Collapsed;
        }

        private void Button_Top_Click(object sender, RoutedEventArgs e)
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            if(appWindow.IsShownInSwitchers)
            {
                var presenter = AppWindow.Presenter as OverlappedPresenter;
                presenter.IsAlwaysOnTop = !presenter.IsAlwaysOnTop;
                ShowSuccess(Helpers.ResourcesHelper.GetString(presenter.IsAlwaysOnTop ? "Setting_AlwaysOnTop" : "Setting_NotAlwaysOnTop"));
            }
        }
    }
}
