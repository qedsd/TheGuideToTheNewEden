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
using TheGuideToTheNewEden.WinUI.Extensions;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class DatabasePage : Page
    {
        private BaseWindow _window;
        public DatabasePage()
        {
            this.InitializeComponent();
            Loaded += DatabasePage_Loaded;
        }

        private void DatabasePage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= DatabasePage_Loaded;
            _window = this.GetBaseWindow();
        }

        private async void SuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if(string.IsNullOrEmpty(sender.Text))
            {
                TextBlock_SearchCount.Text = string.Empty;
                ResultDataGrid.ItemsSource = null;
            }
            else
            {
                object result = null;
                _window?.ShowWaiting();
                try
                {
                    switch (ComboBox_SerachType.SelectedIndex)
                    {
                        case 0:
                            {
                                var list = await Core.Services.DB.InvTypeService.SearchAsync(sender.Text);
                                if(list.NotNullOrEmpty())
                                {
                                    result = await Core.Services.DB.InvTypeService.QueryTypesAsync(list.Select(p=>p.ID).ToList());
                                }
                            }
                            break;
                        case 1:
                            {
                                var list = await Core.Services.DB.InvGroupService.SearchAsync(sender.Text);
                                if (list.NotNullOrEmpty())
                                {
                                    result = await Core.Services.DB.InvGroupService.QueryGroupsAsync(list.Select(p => p.ID).ToList());
                                }
                            }
                            break;
                        case 2:
                            {
                                var list = await Core.Services.DB.MapSolarSystemService.SearchAsync(sender.Text);
                                if (list.NotNullOrEmpty())
                                {
                                    result = await Core.Services.DB.MapSolarSystemService.QueryAsync(list.Select(p => p.ID).ToList());
                                }
                            }
                            break;
                        case 3:
                            {
                                var list = await Core.Services.DB.MapRegionService.SearchAsync(sender.Text);
                                if (list.NotNullOrEmpty())
                                {
                                    result = await Core.Services.DB.MapRegionService.QueryAsync(list.Select(p => p.ID).ToList());
                                }
                            }
                            break;
                        case 4:
                            {
                                var list = await Core.Services.DB.StaStationService.SearchAsync(sender.Text);
                                if (list.NotNullOrEmpty())
                                {
                                    result = await Core.Services.DB.StaStationService.QueryAsync(list.Select(p => p.ID).ToList());
                                }
                            }
                            break;
                        case 5: result = await Core.Services.DB.IDNameDBService.SearchAsync(sender.Text); break;
                        default: result = null; break;
                    }
                }
                catch(Exception ex)
                {
                    Core.Log.Error(ex);
                    _window?.ShowError(ex.Message);
                }
                _window?.HideWaiting();
                ResultDataGrid.ItemsSource = result;
                TextBlock_SearchCount.Text = result == null ? string.Empty : (result as IEnumerable<object>).Count().ToString();
            }
        }
    }
}
