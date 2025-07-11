using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TheGuideToTheNewEden.WPF.Views
{
    public partial class ShellPage : Page, IDisposable
    {
        public ShellPage()
        {
            InitializeComponent();
        }

        public void Dispose()
        {
            //GamePreviewMgrPage.Close();
        }

        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {

        }
    }
}
