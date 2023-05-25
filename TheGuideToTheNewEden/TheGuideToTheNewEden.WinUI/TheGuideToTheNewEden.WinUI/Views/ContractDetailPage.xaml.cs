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
    public sealed partial class ContractDetailPage : Page
    {
        private Core.Models.Contract.ContractInfo _contractInfo;
        public ContractDetailPage(Core.Models.Contract.ContractInfo contractInfo)
        {
            _contractInfo = contractInfo;
            this.InitializeComponent();
            Loaded += ContractDetailPage_Loaded;
        }

        private void ContractDetailPage_Loaded(object sender, RoutedEventArgs e)
        {
            
        }
    }
}
