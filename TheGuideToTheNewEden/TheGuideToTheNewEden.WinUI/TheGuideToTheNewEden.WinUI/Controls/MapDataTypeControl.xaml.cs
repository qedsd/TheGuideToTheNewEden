using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;
using static TheGuideToTheNewEden.WinUI.Controls.MapDataTypeControl;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class MapDataTypeControl : UserControl
    {
        private List<Core.DBModels.MapSolarSystem> _mapSolarSystems;
        public MapDataTypeControl()
        {
            this.InitializeComponent();
            Init();
            DataTypComboBox.SelectionChanged += DataTypComboBox_SelectionChanged;
        }

        private async void Init()
        {
            _mapSolarSystems = (await Core.Services.DB.MapSolarSystemService.QueryAllAsync()).Where(p=> !p.IsSpecial()).ToList();
        }

        #region SOV
        private List<SovData> _sovDatas;
        private void ResetGroupByDefault(List<SovData> sovDatas)
        {
            //TODO
        }
        private void Button_Confirm_Click(object sender, RoutedEventArgs e)
        {
            SOVDatasChanged?.Invoke(_sovDatas);
        }
        public delegate void SOVDatasChangedEventHandel(List<SovData> sovDatas);
        private SOVDatasChangedEventHandel SOVDatasChanged;
        public event SOVDatasChangedEventHandel OnSOVDatasChanged
        {
            add
            {
                SOVDatasChanged += value;
            }
            remove
            {
                SOVDatasChanged -= value;
            }
        }

        public class SovData
        {
            public int AllianceId { get; set; }
            public string AllianceName { get; set; }
            public HashSet<int> SystemIds { get; set; }
            public int GroupId { get; set; }
            public int Count { get => SystemIds.Count; }
        }


        public void SetSOVData(List<SovData> sovDatas)
        {
            _sovDatas = sovDatas;
            ResetGroupByDefault(_sovDatas);
            SOVListDataGrid.ItemsSource = _sovDatas;
        }
        #endregion


        private void DataTypComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SecurityGrid.Visibility = Visibility.Collapsed;
            SOVGrid.Visibility = Visibility.Collapsed;
            PlanetResourcGrid.Visibility = Visibility.Collapsed;
            switch ((sender as ComboBox).SelectedIndex)
            {
                case 0: SecurityGrid.Visibility = Visibility.Visible; break;
                case 1: SOVGrid.Visibility = Visibility.Visible; break;
                case 2: PlanetResourcGrid.Visibility = Visibility.Visible; PlanetResourceTypeChanged?.Invoke(PlanetResourceDataTypeComboBox.SelectedIndex); break;
            }
            DataTypChanged?.Invoke((sender as ComboBox).SelectedIndex);
        }

        public delegate void DataTypChangedEventHandel(int type);
        private DataTypChangedEventHandel DataTypChanged;
        public event DataTypChangedEventHandel OnDataTypChanged
        {
            add
            {
                DataTypChanged += value;
            }
            remove
            {
                DataTypChanged -= value;
            }
        }

        #region ÐÐÐÇ×ÊÔ´
        public delegate void PlanetResourceTypeChangedEventHandel(int type);
        private PlanetResourceTypeChangedEventHandel PlanetResourceTypeChanged;
        public event PlanetResourceTypeChangedEventHandel OnPlanetResourceTypeChanged
        {
            add
            {
                PlanetResourceTypeChanged += value;
            }
            remove
            {
                PlanetResourceTypeChanged -= value;
            }
        }
        private void PlanetResourceDataTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PlanetResourceTypeChanged?.Invoke((sender as ComboBox).SelectedIndex);
        }
        #endregion
    }
}
