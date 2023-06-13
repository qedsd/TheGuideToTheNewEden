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
    public sealed partial class BusinessPage : Page
    {
        private BaseWindow Window;
        public BusinessPage()
        {
            this.InitializeComponent();
            Loaded += BusinessPage_Loaded;
        }

        private void BusinessPage_Loaded(object sender, RoutedEventArgs e)
        {
            Window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
            VM.Window = Window;
        }

        private void CharacterOrderPage_OnSelectedItemsChanged(List<Core.Models.Market.Order> orders)
        {
            Window?.ShowSuccess($"已添加{orders.GroupBy(p => p.TypeId).Count()}个物品到过滤列表");
        }
    }
}
