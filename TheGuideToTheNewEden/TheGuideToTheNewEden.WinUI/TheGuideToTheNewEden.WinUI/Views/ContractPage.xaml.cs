using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ContractPage : Page
    {
        public ContractPage()
        {
            this.InitializeComponent();
            Loaded += ContractPage_Loaded2;
            Loaded += ContractPage_Loaded;
        }
        private void ContractPage_Loaded2(object sender, RoutedEventArgs e)
        {
            VM.Window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
        }
        private void ContractPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ContractPage_Loaded;
            VM.Init();
        }
    }
}
