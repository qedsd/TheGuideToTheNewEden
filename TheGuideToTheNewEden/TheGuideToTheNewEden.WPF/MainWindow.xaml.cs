using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.WPF.Common;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Log.Init();
            ClientServiceHelper.Init(Core.Log.GetLog());
            ActivationService.Init();
            InitializeComponent();
            Closing += MainWindow_Closing2;
            WindowHelper.SetMainWindow(this);
            WindowHelper.TrackWindow(this);
        }
        #region UI定制
        protected override void OnSourceInitialized(EventArgs e)
        {
            SetCurrentValue(WindowStyleProperty, WindowStyle.SingleBorderWindow); 

            System.Windows.Shell.WindowChrome.SetWindowChrome(
                this,
                new System.Windows.Shell.WindowChrome
                {
                    CaptionHeight = 40,
                    CornerRadius = default,
                    GlassFrameThickness = new Thickness(-1),
                    ResizeBorderThickness = ResizeMode == ResizeMode.NoResize ? default : new Thickness(4),
                    UseAeroCaptionButtons = false,
                }
            );
            var handle = WindowHelper.GetWindowHandle(this);
            var windowStyleLong = Win32.GetWindowLong(handle, Win32.GWL_STYLE);
            windowStyleLong &= ~(int)Win32.SYSMENU;

            IntPtr result = Win32.SetWindowLong(handle, Win32.GWL_STYLE, windowStyleLong);

            base.OnSourceInitialized(e);
        }

        private void MinimizeWindow(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeWindow(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                MaximizeIcon.Text = "\uE922";
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                MaximizeIcon.Text = "\uE923";
            }
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void TopWindow(object sender, RoutedEventArgs e)
        {
            if (Topmost)
            {
                Topmost = false;
                TopWindowIcon.Text = "\uE141";
            }
            else
            {
                Topmost = true;
                TopWindowIcon.Text = "\uE196";
            }
        }
        #endregion

        private void MainWindow_Closing2(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ShellPage.Dispose();
        }
    }
}