using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace TheGuideToTheNewEden.UWP.Controls
{
    public sealed partial class WaitingPopup : UserControl
    {
        private Popup Popup;
        public WaitingPopup()
        {
            this.InitializeComponent();
            Popup = new Popup();
            Popup.Child = this;
            this.Width = Window.Current.Bounds.Width;
            this.Height = Window.Current.Bounds.Height;
            Window.Current.SizeChanged += Current_SizeChanged;
        }
        private void Current_SizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            this.Width = e.Size.Width;
            this.Height = e.Size.Height;
        }
        private static WaitingPopup Instance;
        public static void Show()
        {
            if (Instance == null)
            {
                Instance = new WaitingPopup();
            }
            Instance.ProgressRing.IsActive = true;
            Instance.Popup.IsOpen = true;
        }
        public static void Hide()
        {
            if (Instance != null)
            {
                Instance.ProgressRing.IsActive = false;
                Instance.Popup.IsOpen = false;
                Instance = null;
            }
        }
    }
}
