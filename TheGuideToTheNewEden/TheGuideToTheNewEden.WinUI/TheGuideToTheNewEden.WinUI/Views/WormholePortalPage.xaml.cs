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
using TheGuideToTheNewEden.Core.DBModels;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class WormholePortalPage : Page
    {
        public WormholePortalPage()
        {
            this.InitializeComponent();
        }

        public void SetWormholePortal(WormholePortal wormholePortal)
        {
            TextBlock_Name.Text = wormholePortal.Name;
            TextBlock_Destination.Text = Helpers.ResourcesHelper.GetString($"WormholePage_Class_{wormholePortal.Destination}");
            TextBlock_AppearsIn.Text = string.IsNullOrEmpty(wormholePortal.AppearsIn) ? string.Empty : Helpers.ResourcesHelper.GetString($"WormholePage_Class_{wormholePortal.AppearsIn}");
            TextBlock_Lifetime.Text = $"{wormholePortal.Lifetime} {Helpers.ResourcesHelper.GetString("General_Hour")}";
            TextBlock_MaxMassPerJump.Text = wormholePortal.MaxMassPerJump.ToString("N2");
            TextBlock_MaxMassPerJumpNote.Text = wormholePortal.MaxMassPerJumpNote;
            TextBlock_TotalJumpMass.Text = wormholePortal.TotalJumpMass.ToString("N2");
            TextBlock_TotalJumpMassNote.Text = wormholePortal.TotalJumpMassNote;
            TextBlock_Respawn.Text = string.IsNullOrEmpty(wormholePortal.Respawn) ? string.Empty : Helpers.ResourcesHelper.GetString($"WormholePage_Portal_Respawn_{wormholePortal.Respawn}");
            TextBlock_MassRegen.Text = wormholePortal.MassRegen.ToString("N2");
        }
    }
}
