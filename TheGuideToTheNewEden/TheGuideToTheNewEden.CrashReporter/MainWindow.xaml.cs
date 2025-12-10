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

namespace TheGuideToTheNewEden.CrashReporter
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            FlowDocument doc = new FlowDocument();
            Paragraph paragraph = new Paragraph();
            Run run = new Run()
            {
                Text = App.MSG
            };
            paragraph.Inlines.Add(run);
            doc.Blocks.Add(paragraph);
            MsgTextBox.Document = doc;
        }
    }
}