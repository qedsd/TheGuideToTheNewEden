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
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Documents;
using TheGuideToTheNewEden.Core.Extensions;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class TranslationPage : Page
    {
        private TranslationSearchItem _searchItem;
        public TranslationPage()
        {
            this.InitializeComponent();
        }

        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            _searchItem = args.SelectedItem as TranslationSearchItem;
            sender.Text = _searchItem.Name;
            ShowDetail();
        }

        private async void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (_searchItem?.Name == sender.Text)
            {
                return;
            }
            if (string.IsNullOrEmpty(sender.Text))
            {
                sender.ItemsSource = null;
            }
            else
            {
                sender.ItemsSource = await SerachAsync(sender.Text);
            }
        }
        private void ShowDetail()
        {
            RichTextBlock_Desc.Blocks.Clear();
            Image_Type.Visibility = Visibility.Collapsed;
            Paragraph enParagraph, localParagraph;
            string mainName, localName;
            switch(_searchItem.Type)
            {
                case TranslationSearchItem.TranslationSearchType.InvType:
                    {
                        Image_Type.Source = new BitmapImage(new Uri(Converters.GameImageConverter.GetImageUri(_searchItem.ID, Converters.GameImageConverter.ImgType.Type, 64)));
                        Image_Type.Visibility = Visibility.Visible;
                        SearchType(out mainName,out localName, out enParagraph, out localParagraph);
                    }
                    break;
                case TranslationSearchItem.TranslationSearchType.MapRegion:
                    {
                        SearchRegion(out mainName, out localName, out enParagraph, out localParagraph);
                    }
                    break;
                case TranslationSearchItem.TranslationSearchType.MapSolarSystem:
                    {
                        SearchSystem(out mainName, out localName, out enParagraph, out localParagraph);
                    }
                    break;
                case TranslationSearchItem.TranslationSearchType.StaStation:
                    {
                        SearchStation(out mainName, out localName, out enParagraph, out localParagraph);
                    }
                    break;
                default:
                    {
                        mainName = null;
                        localName = null;
                        enParagraph = null;
                        localParagraph = null;
                    }
                    break;
            }
            
            if (enParagraph != null && localParagraph != null)
            {
                localParagraph.Margin = new Thickness(0, 32, 0, 0);
            }
            if (enParagraph != null)
            {
                RichTextBlock_Desc.Blocks.Add(enParagraph);
            }
            if (localParagraph != null)
            {
                RichTextBlock_Desc.Blocks.Add(localParagraph);
            }

            Name_EN.Text = mainName;
            Name_Local.Text = localName;
        }

        private void SearchType(out string mainName, out string localName, out Paragraph enParagraph, out Paragraph localParagraph)
        {
            if (_searchItem.IsLocal)
            {
                var mainType = Core.Services.DB.InvTypeService.QueryType(_searchItem.ID, false);
                if (mainType != null)
                {
                    mainName = mainType.TypeName;
                    enParagraph = Helpers.GameTextHelper.ToParagraph(mainType.Description);
                }
                else
                {
                    mainName = string.Empty;
                    enParagraph = Helpers.GameTextHelper.ToParagraph(string.Empty);
                }
                localName = _searchItem.Name;
                localParagraph = Helpers.GameTextHelper.ToParagraph(_searchItem.Description);
            }
            else
            {
                var localType = Core.Services.DB.LocalDbService.TranInvType(_searchItem.ID);
                if (localType != null)
                {
                    localName = localType.TypeName;
                    localParagraph = Helpers.GameTextHelper.ToParagraph(localType.Description);
                }
                else
                {
                    localName = string.Empty;
                    localParagraph = Helpers.GameTextHelper.ToParagraph(string.Empty);
                }
                mainName = _searchItem.Name;
                enParagraph = Helpers.GameTextHelper.ToParagraph(_searchItem.Description);
            }
        }
        private void SearchRegion(out string mainName, out string localName, out Paragraph enParagraph, out Paragraph localParagraph)
        {
            enParagraph = null;
            localParagraph = null;
            if (_searchItem.IsLocal)
            {
                var mainType = Core.Services.DB.MapRegionService.Query(_searchItem.ID, false);
                if (mainType != null)
                {
                    mainName = mainType.RegionName;
                }
                else
                {
                    mainName = string.Empty;
                }
                localName = _searchItem.Name;
            }
            else
            {
                var localType = Core.Services.DB.LocalDbService.TranMapRegion(_searchItem.ID);
                if (localType != null)
                {
                    localName = localType.RegionName;
                }
                else
                {
                    localName = string.Empty;
                }
                mainName = _searchItem.Name;
            }
        }
        private void SearchSystem(out string mainName, out string localName, out Paragraph enParagraph, out Paragraph localParagraph)
        {
            enParagraph = null;
            localParagraph = null;
            if (_searchItem.IsLocal)
            {
                var mainType = Core.Services.DB.MapSolarSystemService.Query(_searchItem.ID, false);
                if (mainType != null)
                {
                    mainName = mainType.SolarSystemName;
                }
                else
                {
                    mainName = string.Empty;
                }
                localName = _searchItem.Name;
            }
            else
            {
                var localType = Core.Services.DB.LocalDbService.TranMapSolarSystem(_searchItem.ID);
                if (localType != null)
                {
                    localName = localType.SolarSystemName;
                }
                else
                {
                    localName = string.Empty;
                }
                mainName = _searchItem.Name;
            }
        }
        private void SearchStation(out string mainName, out string localName, out Paragraph enParagraph, out Paragraph localParagraph)
        {
            enParagraph = null;
            localParagraph = null;
            if (_searchItem.IsLocal)
            {
                var mainType = Core.Services.DB.StaStationService.Query(_searchItem.ID, false);
                if (mainType != null)
                {
                    mainName = mainType.StationName;
                }
                else
                {
                    mainName = string.Empty;
                }
                localName = _searchItem.Name;
            }
            else
            {
                var localType = Core.Services.DB.LocalDbService.TranStaStation(_searchItem.ID);
                if (localType != null)
                {
                    localName = localType.StationName;
                }
                else
                {
                    localName = string.Empty;
                }
                mainName = _searchItem.Name;
            }
        }

        private async Task<List<TranslationSearchItem>> SerachAsync(string text)
        {
            List<TranslationSearchItem> items = new List<TranslationSearchItem>();
            var types = await Core.Services.DB.InvTypeService.SearchAsync(text);
            if(types.NotNullOrEmpty())
            {
                items.AddRange(types);
            }
            var regions = await Core.Services.DB.MapRegionService.SearchAsync(text);
            if (regions.NotNullOrEmpty())
            {
                items.AddRange(regions);
            }
            var systems = await Core.Services.DB.MapSolarSystemService.SearchAsync(text);
            if (systems.NotNullOrEmpty())
            {
                items.AddRange(systems);
            }
            var stations = await Core.Services.DB.StaStationService.SearchAsync(text);
            if (stations.NotNullOrEmpty())
            {
                items.AddRange(stations);
            }
            return items;
        }
    }
}
