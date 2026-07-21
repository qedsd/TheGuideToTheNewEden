using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.WinUI.Extensions;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class PlanetaryPage : Page, ICharacterPage
    {
        private AuthorizedCharacterData _characterData;
        private PlanetaryService _planetaryService;
        private bool _isLoaded = false;

        public PlanetaryPage()
        {
            this.InitializeComponent();
            Loaded += async (s, e) =>
            {
                if (!_isLoaded) { await RefreshDataAsync(); _isLoaded = true; }
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var paras = e.Parameter as object[];
            if (paras != null && paras.Length >= 2)
            {
                _characterData = paras[1] as AuthorizedCharacterData;
                _planetaryService = new PlanetaryService();
            }
        }

        public void Clear() => _isLoaded = false;
        public void Refresh()
        {
            _isLoaded = false;
            var _ = RefreshDataAsync();
        }

        private async Task RefreshDataAsync()
        {
            if (_characterData == null) return;
            this.ShowWaiting();

            try
            {
                var planets = await _planetaryService.GetCharacterPlanetsAsync(_characterData);

                // 取每个星球的详情来完善数据
                foreach (var p in planets)
                {
                    var detail = await _planetaryService.GetPlanetColonyDetailAsync(_characterData, p.PlanetId);
                    p.NumExtractors = detail.Pins.Count(x => x.Extractor != null);
                    p.NumHeads = detail.Pins.Sum(x => x.Extractor?.NumHeads ?? 0);
                    p.NumActiveExtractors = detail.Pins.Count(x => x.Extractor != null && !x.Extractor.IsExpired);
                    p.NumFactories = detail.Pins.Count(x => x.Category == Core.Models.PlanetColony.PinCategory.BasicIndustryFacility || x.Category == Core.Models.PlanetColony.PinCategory.AdvancedIndustryFacility);
                    var prodNames = detail.Pins.Where(x => !string.IsNullOrEmpty(x.SchematicName)).Select(x => x.SchematicName).ToList();
                    p.ProductionSummary = prodNames.Count > 0 ? string.Join(" ▸ ", prodNames.Take(3)) + (prodNames.Count > 3 ? "..." : "") : "2014";

                    p.NumFactories = detail.Pins.Count(x =>
                        x.Category == Core.Models.PlanetColony.PinCategory.BasicIndustryFacility ||
                        x.Category == Core.Models.PlanetColony.PinCategory.AdvancedIndustryFacility);
                }

                StatColonies.Text = planets.Count.ToString();
                StatExtractors.Text = planets.Sum(p => p.NumExtractors).ToString();
                StatFactories.Text = planets.Sum(p => p.NumFactories).ToString();

                PlanetGrid.ItemsSource = planets;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlanetaryPage] {ex.Message}");
            }
            finally
            {
                this.HideWaiting();
            }
        }
    }
}
