using ESI.NET;
using Microsoft.UI.Xaml;
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
        /// <summary>
        /// 
        /// </summary>
        /// <param name="esiClient"></param>
        /// <param name="contractInfo"></param>
        /// <param name="type">0 公开 1 个人 2 军团</param>
        public ContractDetailWindow(ESI.NET.EsiClient esiClient,Core.Models.Contract.ContractInfo contractInfo, int type)
        {
            HideAppDisplayName();
            SetSmallTitleBar();
            Title = $"{Helpers.ResourcesHelper.GetString("ContractPage_Detail")}-{contractInfo.ContractId}";
            SetHeadText(Title);
            var appWindow = Helpers.WindowHelper.GetAppWindow(this);
            Helpers.WindowHelper.GetAppWindow(this).Resize(new Windows.Graphics.SizeInt32(appWindow.ClientSize.Width / 2, appWindow.ClientSize.Height));
            MainContent = new Views.ContractDetailPage(esiClient, contractInfo, type);
        }
    }
}
