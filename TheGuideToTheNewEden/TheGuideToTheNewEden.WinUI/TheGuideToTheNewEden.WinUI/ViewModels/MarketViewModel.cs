using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class MarketViewModel: BaseViewModel
    {
        private InvType selectedInvType;
        public InvType SelectedInvType
        {
            get => selectedInvType;
            set
            {
                SetProperty(ref selectedInvType, value);
            }
        }
        public MarketViewModel() 
        {

        }
        private void SetSelectedInvType()
        {
            //Core.Services.ESIService.Current.EsiClient.Market.RegionOrders();
        }
    }
}
