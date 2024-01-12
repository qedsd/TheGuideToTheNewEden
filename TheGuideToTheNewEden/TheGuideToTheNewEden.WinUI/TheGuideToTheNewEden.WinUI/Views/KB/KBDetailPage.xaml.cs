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
using TheGuideToTheNewEden.Core.Models.KB;
using Windows.Foundation;
using Windows.Foundation.Collections;
using ZKB.NET.Models.KillStream;

namespace TheGuideToTheNewEden.WinUI.Views.KB
{
    public sealed partial class KBDetailPage : Page
    {
        public KBDetailPage(KBItemInfo kbInfo)
        {
            this.InitializeComponent();
        }
    }
}
