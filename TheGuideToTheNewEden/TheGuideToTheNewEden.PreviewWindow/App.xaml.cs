using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace TheGuideToTheNewEden.PreviewWindow
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string[] Args { get; private set; }
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Args = e.Args;
        }
    }
}
