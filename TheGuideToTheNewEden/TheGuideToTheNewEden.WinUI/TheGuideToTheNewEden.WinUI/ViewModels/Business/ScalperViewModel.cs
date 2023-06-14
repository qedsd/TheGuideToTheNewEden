using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.Market;

namespace TheGuideToTheNewEden.WinUI.ViewModels.Business
{
    public class ScalperViewModel:BaseViewModel
    {
        private MarketLocation sourceMarketLocation;
        public MarketLocation SourceMarketLocation
        {
            get => sourceMarketLocation;
            set => SetProperty(ref  sourceMarketLocation, value);
        }
        private MarketLocation destinationMarketLocation;
        public MarketLocation DestinationMarketLocation
        {
            get => destinationMarketLocation;
            set => SetProperty(ref destinationMarketLocation, value);
        }
    }
}
