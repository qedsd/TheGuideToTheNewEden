using ESI.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.Contract;
using TheGuideToTheNewEden.Core.Models.Market;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class ScalperItemDetailWindow : BaseWindow
    {
        private Views.Business.ScalperItemDetailPage _mainContent;
        public ScalperItemDetailWindow()
        {
            HideAppDisplayName();
            Title = Helpers.ResourcesHelper.GetString("BusinessPage_ScalperItemDetail");
            SetHeadText(Helpers.ResourcesHelper.GetString("BusinessPage_ScalperItemDetail"));
            _mainContent = new Views.Business.ScalperItemDetailPage();
            MainContent = _mainContent;
        }
        public void SetItem(ScalperItem item)
        {
            _mainContent.SetItem(item);
        }
    }
}
