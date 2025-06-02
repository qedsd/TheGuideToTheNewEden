using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Services
{
    public class MarketNavigationService
    {
        private static MarketNavigationService _current;
        public static MarketNavigationService Current
        {
            get
            {
                _current ??= new MarketNavigationService();
                return _current;
            }
        }

        private Views.MarketPage _marketPage;
        public void SetPage(Views.MarketPage marketPage)
        {
            _marketPage = marketPage;
        }

        public void RemovePage(Views.MarketPage marketPage)
        {
            if(_marketPage == marketPage)
            {
                _marketPage = null;
            }
        }

        public void NavigationTo(int typeID)
        {
            if (_marketPage == null)
            {
                _marketPage = new Views.MarketPage();
            }
            NavigationService.SwitchTo(_marketPage, Helpers.ResourcesHelper.GetString("ShellPage_Market"));
            _marketPage.ViewType(typeID);
        }
    }
}
