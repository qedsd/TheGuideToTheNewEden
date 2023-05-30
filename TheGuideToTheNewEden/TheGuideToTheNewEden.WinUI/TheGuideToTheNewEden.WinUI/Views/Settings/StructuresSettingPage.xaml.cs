// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.Core.Models.Universe;
using Syncfusion.UI.Xaml.DataGrid;

namespace TheGuideToTheNewEden.WinUI.Views.Settings
{
    public sealed partial class StructuresSettingPage : Page
    {
        public StructuresSettingPage()
        {
            this.InitializeComponent();
            Loaded += StructuresSettingPage_Loaded;
        }

        private void SerachDataGrid_SelectionChanged(object sender, Syncfusion.UI.Xaml.Grids.GridSelectionChangedEventArgs e)
        {
            VM.SelectedSearchStructures = (sender as SfDataGrid).SelectedItems?.Select(p => p as Structure).ToList();
        }

        private void StructuresSettingPage_Loaded(object sender, RoutedEventArgs e)
        {
            VM.Window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
        }

        private void StructuresListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            VM.SelectedStructures = (sender as ListView).SelectedItems.Select(p => p as Structure).ToList();
        }
    }
}
