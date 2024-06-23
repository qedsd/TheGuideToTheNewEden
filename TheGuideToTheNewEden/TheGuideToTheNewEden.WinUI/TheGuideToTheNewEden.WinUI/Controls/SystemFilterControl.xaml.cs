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
    public sealed partial class SystemFilterControl : UserControl
    {
        private List<Core.DBModels.MapSolarSystem> _mapSolarSystems;
        private List<Core.DBModels.MapRegion> _mapRegions;
        private List<ToggleButton> _regionToggleButton = new List<ToggleButton>();
        private HashSet<int> _selectedRegions = new HashSet<int>();
        private HashSet<int> _selectedSystems = new HashSet<int>();
        public SystemFilterControl()
        {
            this.InitializeComponent();
            Init();
        }
        private async void Init()
        {
            _mapSolarSystems = (await Core.Services.DB.MapSolarSystemService.QueryAllAsync()).Where(p=> !p.IsSpecial()).ToList();
            TextBlock_AllFilteredSystemCount.Text = _mapSolarSystems.Count.ToString();
            TextBlock_FilteredSystemCount.Text = TextBlock_AllFilteredSystemCount.Text;
            _mapRegions = (await Core.Services.DB.MapRegionService.QueryAllAsync()).Where(p => !p.IsSpecial()).ToList();
            GridView_Region.ItemsSource = _mapRegions;
            TextBlock_AllRegionCount.Text = _mapRegions.Count.ToString();
            TextBlock_SelectedRegionCount.Text = _mapRegions.Count.ToString();
            TextBlock_AllSystemCount.Text = _mapSolarSystems.Count.ToString();
            TextBlock_SelectedSystemCount.Text = "0";
            foreach (var region in _mapRegions)
            {
                _selectedRegions.Add(region.RegionID);
            }
            GridView_System.ItemsSource = new ObservableCollection<Core.DBModels.MapSolarSystem>();
            //await InitSovList();
        }
        #region ����
        private void Button_SelecteAllRegion_Click(object sender, RoutedEventArgs e)
        {
            bool b = (sender as ToggleButton).IsChecked == true;
            foreach (var button in _regionToggleButton)
            {
                button.IsChecked = b;
            }
            if(b)
            {
                foreach(var region in _mapRegions)
                {
                    _selectedRegions.Add(region.RegionID);
                }
            }
            else
            {
                _selectedRegions.Clear();
            }
            TextBlock_SelectedRegionCount.Text = _selectedRegions.Count.ToString();
        }

        private void Button_Region_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as ToggleButton;
            var region = button.DataContext as Core.DBModels.MapRegion;
            if (button.IsChecked == true)
            {
                _selectedRegions.Add(region.RegionID);
            }
            else
            {
                _selectedRegions.Remove(region.RegionID);
            }
            TextBlock_SelectedRegionCount.Text = _selectedRegions.Count.ToString();
        }

        private void ToggleButton_Region_Loaded(object sender, RoutedEventArgs e)
        {
            _regionToggleButton.Add(sender as ToggleButton);
        }
        #endregion
        #region ��ϵ
        private void MapSystemSelector_OnSelectedItemChanged(Core.DBModels.MapSolarSystem selectedItem)
        {
            if(selectedItem != null)
            {
                if (_selectedSystems.Add(selectedItem.SolarSystemID))
                {
                    (GridView_System.ItemsSource as ObservableCollection<Core.DBModels.MapSolarSystem>).Add(selectedItem);
                    TextBlock_SelectedSystemCount.Text = _selectedSystems.Count.ToString();
                }
            }
        }

        private void GridView_System_ItemClick(object sender, ItemClickEventArgs e)
        {
            var system = e.ClickedItem as Core.DBModels.MapSolarSystem;
            _selectedSystems.Remove(system.SolarSystemID);
            (GridView_System.ItemsSource as ObservableCollection<Core.DBModels.MapSolarSystem>).Remove(system);
            TextBlock_SelectedSystemCount.Text = _selectedSystems.Count.ToString();
        }
        #endregion

        #region SOV
        private List<SovData> _sovDatas;
        private HashSet<int> _selectedAlliances = new HashSet<int>();
        private void Button_SelecteAllSOV_Click(object sender, RoutedEventArgs e)
        {
            if((sender as ToggleButton).IsChecked == true)
            {
                ListView_SOV.SelectAll();
            }
            else
            {
                ListView_SOV.SelectedIndex = -1;
            }
            TextBlock_SelectedSOVCount.Text = _selectedAlliances.Count.ToString();
        }

        private void ListView_SOV_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.NotNullOrEmpty())
            {
                foreach (var item in e.RemovedItems)
                {
                    _selectedAlliances.Remove((item as SovData).AllianceId);
                }
            }
            if (e.AddedItems.NotNullOrEmpty())
            {
                foreach (var item in e.AddedItems)
                {
                    _selectedAlliances.Add((item as SovData).AllianceId);
                }
            }
            TextBlock_SelectedSOVCount.Text = _selectedAlliances.Count.ToString();
        }

        public void SetSOVData(List<SovData> sovDatas)
        {
            _sovDatas = sovDatas;
            TextBlock_AllSOVCount.Text = _sovDatas.Count.ToString();
            ListView_SOV.ItemsSource = _sovDatas;
        }
        #endregion

        private void Button_Confirm_Click(object sender, RoutedEventArgs e)
        {
            var ids = Cal();
            TextBlock_FilteredSystemCount.Text = ids.Count.ToString();
            FilterSystemChanged?.Invoke(ids.ToHashSet2());
        }
        private List<int> Cal()
        {
            //������ʾ����ϵid
            var allSystemDic = _mapSolarSystems.ToDictionary(p => p.SolarSystemID);
            Dictionary<int, MapSolarSystem> filtedSystems = new Dictionary<int, MapSolarSystem>();
            //����
            if (_selectedRegions.Count == 0)
            {
                return null;
            }
            else
            {
                foreach(var system in _mapSolarSystems)
                {
                    if(_selectedRegions.Contains(system.RegionID))
                    {
                        filtedSystems.Add(system.SolarSystemID, system);
                    }
                }
            }
            //��ϵ
            if(SystemModeComboBox.SelectedIndex == 0 )//�ų��б�
            {
                foreach(var sys in _selectedSystems)
                {
                    filtedSystems.Remove(sys);
                }
            }
            else//�����б�
            {
                filtedSystems.Clear();
                foreach (var sys in _selectedSystems)
                {
                    filtedSystems.Add(sys, allSystemDic[sys]);
                }
            }
            //��Ȩ
            if(ContainNoneSOVCheckBox.IsChecked == false)//����������Ȩ��ϵ�����Ƴ����в�����Ȩ�µ���ϵ
            {
                HashSet<int> allInSovSystems = new HashSet<int>();//��������Ȩ����ϵid
                foreach (var sovData in _sovDatas)
                {
                    foreach (var sys in sovData.SystemIds)
                    {
                        allInSovSystems.Add(sys);
                    }
                }
                List<int> removedIds = new List<int>();
                foreach (var sys in filtedSystems)
                {
                    if (!allInSovSystems.Contains(sys.Key))
                    {
                        removedIds.Add(sys.Key);
                    }
                }
                foreach (var id in removedIds)
                {
                    filtedSystems.Remove(id);
                }
            }
            //��������������Ȩ��ϵ��������ѡ�����Ȩ��ϵ���ų�ûѡ���
            if (_selectedAlliances.Count != _sovDatas.Count)//������ûѡ��ʱ����Ҫȫ���ж�һ��
            {
                foreach (var sovData in _sovDatas)
                {
                    if (!_selectedAlliances.Contains(sovData.AllianceId))//���˲�ѡ��ʱ��ɾ��������ϵ
                    {
                        foreach (var sys in sovData.SystemIds)
                        {
                            filtedSystems.Remove(sys);
                        }
                    }
                }
            }

            //��ȫ�ȼ�
            if (SecuritySlider.RangeStart != SecuritySlider.Minimum || SecuritySlider.RangeEnd != SecuritySlider.Maximum)
            {
                //����ѡ��Χ�������Χʱ����Ҫ�ж�һ��
                List<int> removedIds = new List<int>();
                foreach (var sys in filtedSystems)
                {
                    if (sys.Value.Security < SecuritySlider.RangeStart || sys.Value.Security > SecuritySlider.RangeEnd)
                    {
                        removedIds.Add(sys.Key);
                    }
                }
                foreach (var id in removedIds)
                {
                    filtedSystems.Remove(id);
                }
            }
            return filtedSystems.Keys.ToList();
        }
        public delegate void FilterSystemChangedEventHandel(HashSet<int> ids);
        private FilterSystemChangedEventHandel FilterSystemChanged;
        public event FilterSystemChangedEventHandel OnFilterSystemChanged
        {
            add
            {
                FilterSystemChanged += value;
            }
            remove
            {
                FilterSystemChanged -= value;
            }
        }
    }
}
