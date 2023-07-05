using ESI.NET;
using ESI.NET.Models.Fleets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class MailWindow : BaseWindow
    {
        public MailWindow(EsiClient esiClient, Core.Models.Mail.MailDetail mailDetail) 
        {
            SetSmallTitleBar();
            Title = Helpers.ResourcesHelper.GetString("MailPage_Mail");
            SetHeadText(Helpers.ResourcesHelper.GetString("MailPage_Mail"));
            var appWindow = Helpers.WindowHelper.GetAppWindow(this);
            Helpers.WindowHelper.GetAppWindow(this).Resize(new Windows.Graphics.SizeInt32(appWindow.ClientSize.Width / 2, appWindow.ClientSize.Height));
            MainContent = new Views.Character.MailDetailPage(esiClient, mailDetail);
        }
    }
}
