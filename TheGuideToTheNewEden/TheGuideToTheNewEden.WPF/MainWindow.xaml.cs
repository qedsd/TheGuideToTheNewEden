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
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            ActivationService.Init();
            Log.Init();
            InitializeComponent();
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            GamePreviewMgrPage.VM.Dispose();
        }
    }
}