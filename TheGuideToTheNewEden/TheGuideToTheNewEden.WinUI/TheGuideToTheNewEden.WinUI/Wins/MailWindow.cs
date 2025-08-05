using ESI.NET;
using ESI.NET.Models.Fleets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class MailWindow : ToolWindow
    {
        public MailWindow(EsiClient esiClient, Core.Models.Mail.MailDetail mailDetail) 
        {
            var appWindow = Helpers.WindowHelper.GetAppWindow(this);
            Helpers.WindowHelper.GetAppWindow(this).Resize(new Windows.Graphics.SizeInt32(appWindow.ClientSize.Width / 2, appWindow.ClientSize.Height));
            var content = new Views.Character.MailDetailPage(esiClient, mailDetail);
            InitWindow(content, WindowTitleStyle.Default, true, true, true, true);
            SetWindowTitle(Helpers.ResourcesHelper.GetString("MailPage_Mail"));
            SetDisplayTitle(Helpers.ResourcesHelper.GetString("MailPage_Mail"));
        }
    }
}
