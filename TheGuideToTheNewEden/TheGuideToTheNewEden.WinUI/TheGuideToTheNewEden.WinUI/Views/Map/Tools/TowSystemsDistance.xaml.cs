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
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Views.Map.Tools
{
    public sealed partial class TowSystemsDistance : Page
    {
        private Core.EVEHelpers.ShortestPathHelper _shortestPathHelper;
        public TowSystemsDistance()
        {
            this.InitializeComponent();
        }
        private Core.DBModels.MapSolarSystem _system1;
        private Core.DBModels.MapSolarSystem _system2;
        private void MapSystemSelector1_OnSelectedItemChanged(Core.DBModels.MapSolarSystem selectedItem)
        {
            _system1 = selectedItem;
            Cal();
        }

        private void MapSystemSelector2_OnSelectedItemChanged(Core.DBModels.MapSolarSystem selectedItem)
        {
            _system2 = selectedItem;
            Cal();
        }
        private async void Cal()
        {
            if(_system1 != null && _system2 != null && _system1.SolarSystemID != _system2.SolarSystemID)
            {
                var m = Math.Sqrt(Math.Pow(_system1.X - _system2.X, 2) + Math.Pow(_system1.Y - _system2.Y, 2) + Math.Pow(_system1.Z - _system2.Z, 2));
                var ly = m / 9460730472580800;
                LYTextBlock.Text = ly.ToString("N2");
                CalProgressBar.IsIndeterminate = true;
                CalProgressBar.Visibility = Visibility.Visible;
                List<int> paths = null;
                await Task.Run(() =>
                {
                    try
                    {
                        if (_shortestPathHelper == null)
                        {
                            _shortestPathHelper = new Core.EVEHelpers.ShortestPathHelper();
                        }
                        paths = _shortestPathHelper.Cal(_system1.SolarSystemID, _system2.SolarSystemID);
                    }
                    catch(Exception ex)
                    {
                        Core.Log.Error(ex);
                    }
                });
                CalProgressBar.IsIndeterminate = false;
                CalProgressBar.Visibility = Visibility.Collapsed;
                JumpsTextBlock.Text = paths == null ? "-" : paths.Count.ToString();
            }
            else
            {
                LYTextBlock.Text = "-";
                JumpsTextBlock.Text = "-";
            }
        }
    }
}
