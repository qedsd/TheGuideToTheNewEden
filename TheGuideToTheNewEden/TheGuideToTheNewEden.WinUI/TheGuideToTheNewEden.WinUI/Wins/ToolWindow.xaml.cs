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
using TheGuideToTheNewEden.WinUI.Controls;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Interfaces;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using TheGuideToTheNewEden.WinUI.Views;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.WindowManagement;
using WinRT;
using WinUIEx;
using static TheGuideToTheNewEden.WinUI.Controls.InfoBarControl;

namespace TheGuideToTheNewEden.WinUI
{
    public partial class ToolWindow : Window, IWindow
    {
        private bool _canClose = true;

        /// <summary>
        /// 无缩放下的原始标题栏高度
        /// </summary>
        public int TitleBarHeight { get; private set; }
        public UIElement MainUIElement
        {
            get => MainWindowGrid;
        }
        public ToolWindow()
        {
            this.InitializeComponent();
        }
        public ToolWindow(UIElement content, WindowTitleStyle style, bool showTopButton, bool showInSwitcher)
        {
            this.InitializeComponent();
            InitWindow(content, style, showTopButton, true, true, showInSwitcher);
        }
        public ToolWindow(string displayTitle, string windowTitle, UIElement content, WindowTitleStyle style, bool showTopButton, bool useThemeService, bool useBackground, bool showInSwitcher, bool top, int defaultWidth, int defaultHight)
        {
            this.InitializeComponent();
            InitWindow(content, style, showTopButton, useThemeService, useBackground, showInSwitcher, defaultWidth, defaultHight);
            SetDisplayTitle(displayTitle);
            SetWindowTitle(windowTitle);
            if (top)
            {
                SetAlwaysOnTop();
            }
        }
        public void InitWindow(UIElement content, WindowTitleStyle style, bool showTopButton, bool useThemeService, bool useBackground, bool showInSwitcher, int width = 800, int hight = 600)
        {
            if (useThemeService)
            {
                ThemeService = new DevWinUI.ThemeService();
                ThemeService.Initialize(this);
                ThemeService.ConfigureElementTheme(ThemeSelectorService.Theme);
                ThemeService.ConfigureBackdrop(BackdropSelectorService.GetDevWinUIBackdropTypeValue());
                ThemeSelectorService.OnChangedTheme += ThemeSelectorService_OnChangedTheme;
                ThemeSelectorService_OnChangedTheme(ThemeSelectorService.Theme);
                Closed += ToolWindow_Closed;
                BackdropSelectorService.OnBackdropTypeChanged += BackdropSelectorService_OnBackdropTypeChanged;
                BackdropSelectorService.OnCustomPictureFileChanged += BackdropSelectorService_OnCustomPictureFileChanged;
                BackdropSelectorService.OnCustomPictureOverlapColorChanged += BackdropSelectorService_OnCustomPictureOverlapColorChanged;
            }
            TitleBarHeight = (int)(WindowHelper.GetTitleBarHeight(WindowHelper.GetWindowHandle(this)) / Helpers.WindowHelper.GetDpiScale(this));//只能在ExtendsContentIntoTitleBar前获取，之后会变为0
            this.Title = Helpers.ResourcesHelper.GetString("AppDisplayName");
            Helpers.WindowHelper.TrackWindow(this);

            //Helpers.WindowHelper.CenterToScreen(this);
            WindowHelper.GetAppWindow(this).SetIcon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico"));
            if (useBackground && BackdropSelectorService.BackdropTypeValue == BackdropSelectorService.BackdropType.CustomPicture)
            {
                if (!string.IsNullOrEmpty(BackdropSelectorService.CustomPictureFileValue))
                {
                    LoadCustomPicture();
                }
            }
            ExtendsContentIntoTitleBar = true;
            if (style != WindowTitleStyle.Empty)
                SetTitleBar(AppTitleBar);
            AppWindow.Resize(new Windows.Graphics.SizeInt32(width,hight));
            AppWindow.IsShownInSwitchers = showInSwitcher;

            HideNavButton();
            if (style != WindowTitleStyle.Default)
            {
                switch (style)
                {
                    case WindowTitleStyle.OnlyClose: MinimizeButton.Visibility = Visibility.Collapsed; MaximizeButton.Visibility = Visibility.Collapsed; break;
                    case WindowTitleStyle.OnlyMini: CloseButton.Visibility = Visibility.Collapsed; MaximizeButton.Visibility = Visibility.Collapsed; break;
                    case WindowTitleStyle.OnlyMax: CloseButton.Visibility = Visibility.Collapsed; MinimizeButton.Visibility = Visibility.Collapsed; break;
                    case WindowTitleStyle.NoButton: ButtonPanel.Visibility = Visibility.Collapsed; break;
                    case WindowTitleStyle.Empty: FullTitleArea.Visibility = Visibility.Collapsed; break;
                    case WindowTitleStyle.MiniAndClose: MaximizeButton.Visibility = Visibility.Collapsed; break;
                }
            }
            if (!showTopButton || style == WindowTitleStyle.Empty)
            {
                TopButton.Visibility = Visibility.Collapsed;
            }
            ContentFrame.Content = content;
        }

        private void ToolWindow_Closed(object sender, WindowEventArgs args)
        {
            ThemeSelectorService.OnChangedTheme -= ThemeSelectorService_OnChangedTheme;
            BackdropSelectorService.OnBackdropTypeChanged -= BackdropSelectorService_OnBackdropTypeChanged;
            BackdropSelectorService.OnCustomPictureFileChanged -= BackdropSelectorService_OnCustomPictureFileChanged;
            BackdropSelectorService.OnCustomPictureOverlapColorChanged -= BackdropSelectorService_OnCustomPictureOverlapColorChanged;
        }

        public void HideNavButton()
        {
            var presenter = Helpers.WindowHelper.GetOverlappedPresenter(this);
            presenter.SetBorderAndTitleBar(true, false);
            AppTitleBarGrid.Margin = new Thickness(0);
        }
       
        public void Hide()
        {
            this.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                WindowHelper.GetAppWindow(this)?.Hide();
            });
        }

        /// <summary>
        /// 界面显示的标题
        /// </summary>
        /// <param name="title"></param>
        public void SetDisplayTitle(string title)
        {
            TitleTextBlock.Text = title;
            Title = title;
        }

        /// <summary>
        /// 系统窗口显示的窗口名称
        /// </summary>
        /// <param name="title"></param>
        public void SetWindowTitle(string title)
        {
            Title = title;
        }

        public void SetSize(int w, int h)
        {
            AppWindow.Resize(new Windows.Graphics.SizeInt32(w, h));
        }
        public void SetPosition(int x, int y)
        {
            Helpers.WindowHelper.MoveToScreen(this, x, y);
        }
        public void SetAlwaysOnTop()
        {
            this.SetIsAlwaysOnTop(true);
        }

        public void SetCloseToHide()
        {
            _canClose = false;
        }
        #region 主题
        public DevWinUI.IThemeService ThemeService { get; set; }
        private void ThemeSelectorService_OnChangedTheme(ElementTheme theme)
        {
            Microsoft.UI.Windowing.AppWindowTitleBar titleBar = AppWindow.TitleBar;
            switch (theme)
            {
                case ElementTheme.Dark:
                    {
                        if (BackdropSelectorService.BackdropTypeValue == BackdropSelectorService.BackdropType.None)
                        {
                            MainWindowGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32));
                        }
                        titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
                    }
                    break;
                case ElementTheme.Light:
                    {
                        if (BackdropSelectorService.BackdropTypeValue == BackdropSelectorService.BackdropType.None)
                            MainWindowGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 243, 243));
                        titleBar.ButtonForegroundColor = Microsoft.UI.Colors.Black;
                    }
                    break;
                case ElementTheme.Default:
                    {
                        if (Application.Current.RequestedTheme == ApplicationTheme.Dark)
                        {
                            if (BackdropSelectorService.BackdropTypeValue == BackdropSelectorService.BackdropType.None)
                                MainWindowGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32));
                            titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
                        }
                        else
                        {
                            if (BackdropSelectorService.BackdropTypeValue == BackdropSelectorService.BackdropType.None)
                                MainWindowGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 243, 243));
                            titleBar.ButtonForegroundColor = Microsoft.UI.Colors.Black;
                        }
                    }
                    break;
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
        #endregion

        #region 标题按钮
        private void Button_Top_Click(object sender, RoutedEventArgs e)
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            if(appWindow.IsShownInSwitchers)
            {
                var presenter = AppWindow.Presenter as OverlappedPresenter;
                presenter.IsAlwaysOnTop = !presenter.IsAlwaysOnTop;
            }
        }

        private void MinimizeWindow(object sender, RoutedEventArgs e)
        {
            this.Minimize();
        }

        private void MaximizeWindow(object sender, RoutedEventArgs e)
        {
            if(AppWindow.Presenter is OverlappedPresenter presenter)
            {
                if(presenter.State == OverlappedPresenterState.Maximized)
                {
                    this.Restore();
                }
                else
                {
                    this.Maximize();
                }
            }
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            if (_canClose)
            {
                this.Close();
            }
            else
            {
                this.Hide();
            }
        }

        public void ShowMsg(string msg, bool autoClose = true)
        {
            this.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                InfoBar.Show(this.Title, msg, InfoType.Info, autoClose, null);
            });
        }

        public void ShowError(string msg, bool autoClose = true)
        {
            this.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                InfoBar.Show(this.Title, msg, InfoType.Error, autoClose, null);
            });
        }

        public void ShowSuccess(string msg, bool autoClose = true)
        {
            this.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                InfoBar.Show(this.Title, msg, InfoType.Success, autoClose, null);
            });
        }

        public void ShowWaiting(string tip = null)
        {
            this.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                Loading.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                Loading.IsLoading = true;
                Loading.LoadingContent = tip;
                ContentFrame.IsEnabled = false;
            });
        }

        public void HideWaiting()
        {
            this.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                Loading.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                Loading.IsLoading = false;
                ContentFrame.IsEnabled = true;
            });
        }

        public Window GetWindow()
        {
            return this;
        }
        #endregion
    }

    public enum WindowTitleStyle
    {
        Default,
        OnlyClose,
        OnlyMini,
        OnlyMax,
        NoButton,
        Empty,
        MiniAndClose
    }
}
