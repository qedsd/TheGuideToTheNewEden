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

namespace TheGuideToTheNewEden.WinUI
{
    public partial class MainWindow : Window, IWindow
    {
        public DevWinUI.IThemeService ThemeService { get; set; }
        public MainWindow()
        {
            Init(true, true, false);
        }
        public MainWindow(bool useThemeService)
        {
            Init(useThemeService, true, false);
        }
        public MainWindow(bool useThemeService, bool useBackgroun)
        {
            Init(useThemeService, useBackgroun, false);
        }
        public MainWindow(bool useThemeService, bool useBackgroun, bool hideCaptionButton)
        {
            Init(useThemeService, useBackgroun, hideCaptionButton);
        }
        public void Init(bool useThemeService, bool useBackground, bool hideCaptionButton)
        {
            this.InitializeComponent();
            this.LogPositionAndSize();
            if (useThemeService)
            {
                ThemeService = new DevWinUI.ThemeService();
                ThemeService.Initialize(this, false);
                ThemeService.ConfigureElementTheme(ThemeSelectorService.Theme);
                ThemeService.ConfigureBackdrop();
                ThemeSelectorService.OnChangedTheme += ThemeSelectorService_OnChangedTheme;
                ThemeSelectorService_OnChangedTheme(ThemeSelectorService.Theme);
                BackdropSelectorService.OnBackdropTypeChanged += BackdropSelectorService_OnBackdropTypeChanged;
                BackdropSelectorService.OnCustomPictureFileChanged += BackdropSelectorService_OnCustomPictureFileChanged;
                BackdropSelectorService.OnCustomPictureOverlapColorChanged += BackdropSelectorService_OnCustomPictureOverlapColorChanged;
            }
            TitleBarHeight = (int)(WindowHelper.GetTitleBarHeight(WindowHelper.GetWindowHandle(this)) / Helpers.WindowHelper.GetDpiScale(this));//只能在ExtendsContentIntoTitleBar前获取，之后会变为0
            this.Title = Helpers.ResourcesHelper.GetString("AppDisplayName");
            Helpers.WindowHelper.TrackWindow(this);

            WindowHelper.GetAppWindow(this).SetIcon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico"));
            if (useBackground && BackdropSelectorService.BackdropTypeValue == BackdropSelectorService.BackdropType.CustomPicture)
            {
                if (!string.IsNullOrEmpty(BackdropSelectorService.CustomPictureFileValue))
                {
                    LoadCustomPicture();
                }
            }
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
        }
        public UIElement MainUIElement
        {
            get => MainWindowGrid;
        }

        /// <summary>
        /// 无缩放下的原始标题栏高度
        /// </summary>
        public int TitleBarHeight { get;private set; }

        public void HideNavButton()
        {
            var presenter = Helpers.WindowHelper.GetOverlappedPresenter(this);
            presenter.SetBorderAndTitleBar(true, false);
            TitleBarExtendsToNavButton();
        }
        /// <summary>
        /// 将TitleBar拖动区域扩展到原生导航键（最小最大关闭）位置
        /// </summary>
        public void TitleBarExtendsToNavButton()
        {
            //AppTitleBarGrid.Margin = new Thickness(0);
        }
       
        public void Hide()
        {
            this.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                WindowHelper.GetAppWindow(this)?.Hide();
            });
        }

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

        public void ShowMsg(string msg, bool autoClose = true)
        {
            throw new NotImplementedException();
        }

        public void ShowError(string msg, bool autoClose = false)
        {
            throw new NotImplementedException();
        }

        public void ShowSuccess(string msg, bool autoClose = true)
        {
            throw new NotImplementedException();
        }

        public void ShowWaiting(string tip = null)
        {
            throw new NotImplementedException();
        }

        public void HideWaiting()
        {
            throw new NotImplementedException();
        }
        public Window GetWindow()
        {
            return this;
        }
    }
}
