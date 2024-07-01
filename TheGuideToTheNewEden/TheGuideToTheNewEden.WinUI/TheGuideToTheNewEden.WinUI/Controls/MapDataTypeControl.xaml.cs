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
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class MapDataTypeControl : UserControl
    {
        public MapDataTypeControl()
        {
            this.InitializeComponent();
            DataTypComboBox.SelectionChanged += DataTypComboBox_SelectionChanged;
        }

        #region SOV
        private List<SovData> _sovDatas;
        private void ResetGroupByDefault(List<SovData> sovDatas)
        {
            if(sovDatas.NotNullOrEmpty())
            {
                var dic = ReadSOVGroup();
                if (dic.NotNullOrEmpty())
                {
                    foreach (var data in sovDatas)
                    {
                        if (dic.TryGetValue(data.AllianceId, out int groupID))
                        {
                            data.GroupId = groupID;
                        }
                    }
                }
                int maxGroup = sovDatas.Max(p => p.GroupId);
                foreach (var data in sovDatas)
                {
                    if (data.GroupId < 1)
                    {
                        data.GroupId = ++maxGroup;
                    }
                }
            }
        }
        private void Button_Confirm_Click(object sender, RoutedEventArgs e)
        {
            SaveSOVGroup(_sovDatas);
            SOVDatasChanged?.Invoke(_sovDatas);
        }
        private void Button_ResetGroup_Click(object sender, RoutedEventArgs e)
        {
            int id = 1;
            foreach(var data in _sovDatas)
            {
                data.GroupId = id++;
            }
            SOVListDataGrid.ItemsSource = null;
            SOVListDataGrid.ItemsSource = _sovDatas;
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


        private static readonly string SOVGroupPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "SOVGroup.json");
        public void SetSOVData(List<SovData> sovDatas)
        {
            _sovDatas = sovDatas;
            ResetGroupByDefault(_sovDatas);
            SOVListDataGrid.ItemsSource = _sovDatas;
        }

        private void SaveSOVGroup(List<SovData> sovDatas)
        {
            if(sovDatas.NotNullOrEmpty())
            {
                List<string> lines = new List<string>();
                foreach(var data in sovDatas)
                {
                    lines.Add($"{data.AllianceId} {data.GroupId}");
                }
                File.WriteAllLines(SOVGroupPath, lines);
            }
        }
        private Dictionary<int,int> ReadSOVGroup()
        {
            try
            {
                if (File.Exists(SOVGroupPath))
                {
                    var lines = File.ReadAllLines(SOVGroupPath);
                    if (lines.NotNullOrEmpty())
                    {
                        Dictionary<int, int> dic = new Dictionary<int, int>();
                        foreach (var line in lines)
                        {
                            var array = line.Split(' ');
                            dic.Add(int.Parse(array[0]), int.Parse(array[1]));
                        }
                        return dic;
                    }
                }
                return null;
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                return null;
            }
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
