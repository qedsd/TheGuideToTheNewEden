using ESI.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.Mail;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class ContractDetailWindow : BaseWindow
    {
        public ContractDetailWindow(Core.Models.Contract.ContractInfo contractInfo)
        {
            SetSmallTitleBar();
            SetHeadText(Helpers.ResourcesHelper.GetString("ContractPage_Detail"));
            Helpers.WindowHelper.GetAppWindow(this).Resize(new Windows.Graphics.SizeInt32(600, 800));
            MainContent = new Views.ContractDetailPage();
        }
    }
}
