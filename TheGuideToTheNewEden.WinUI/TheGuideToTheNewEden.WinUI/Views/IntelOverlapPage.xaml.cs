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
    public sealed partial class IntelOverlapPage : Page
    {
        public Canvas MapCanvas { get => mapCanvas; }
        public Canvas LineCanvas { get => lineCanvas; }
        public Canvas TempCanvas { get => tempCanvas; }
        public Grid MapGrid { get => mapGrid; }
        public TextBlock TipTextBlock { get => tipTextBlock; }
        public TextBlock InfoTextBlock { get => infoTextBlock; }
        public IntelOverlapPage()
        {
            this.InitializeComponent();
        }
    }
}
