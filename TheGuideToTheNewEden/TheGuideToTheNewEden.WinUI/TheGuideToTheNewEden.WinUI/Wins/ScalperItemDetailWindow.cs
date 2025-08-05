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
    internal class ScalperItemDetailWindow : ToolWindow
    {
        private Views.Business.ScalperItemDetailPage _mainContent;
        public ScalperItemDetailWindow()
        {
            _mainContent = new Views.Business.ScalperItemDetailPage();
            InitWindow(_mainContent, WindowTitleStyle.Empty, false, true, true, true);
            SetDisplayTitle(Helpers.ResourcesHelper.GetString("BusinessPage_ScalperItemDetail"));
            SetWindowTitle(Helpers.ResourcesHelper.GetString("BusinessPage_ScalperItemDetail"));
        }
        public void SetItem(ScalperItem item)
        {
            _mainContent.SetItem(item);
        }
    }
}
