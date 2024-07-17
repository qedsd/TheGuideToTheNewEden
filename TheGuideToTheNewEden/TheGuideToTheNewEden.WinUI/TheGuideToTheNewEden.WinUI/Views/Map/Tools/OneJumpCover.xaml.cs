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
using TheGuideToTheNewEden.Core.Models;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views.Map.Tools
{
    public sealed partial class OneJumpCover : Page
    {
        public OneJumpCover()
        {
            this.InitializeComponent();
            Loaded += OneJumpCover_Loaded;
        }

        private void OneJumpCover_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OneJumpCover_Loaded;
            ShipTypeComboBox.ItemsSource = Core.EVEHelpers.CapitalJumpShipInfoHelper.GetInfos();
        }

        private void ShipTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateShipMaxJump();
        }

        private void JumpDriveCalibrationNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            UpdateShipMaxJump();
        }
        private void UpdateShipMaxJump()
        {
            CapitalJumpShipInfo ship = ShipTypeComboBox.SelectedItem as CapitalJumpShipInfo;
            if (ship != null)
            {
                MaxJumpTextBlock.Text = GetShipMaxJump(ship).ToString("N2");
            }
        }
        private double GetShipMaxJump(CapitalJumpShipInfo ship)
        {
            return ship.MaxLY + ship.MaxLY * 0.2 * (int)JumpDriveCalibrationNumberBox.Value;
        }
    }
}
