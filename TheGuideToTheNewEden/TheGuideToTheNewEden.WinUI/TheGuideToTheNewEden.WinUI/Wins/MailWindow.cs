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
        public MailWindow(EsiClient esiClient, ESI.NET.Models.Mail.Message message) 
        {
            SetSmallTitleBar();
            HideAppDisplayName();
            SetHeadText(message.Subject);
            Helpers.WindowHelper.GetAppWindow(this).Resize(new Windows.Graphics.SizeInt32(400, 600));
            MainContent = new Views.Character.MailDetailPage(esiClient, message);
        }
    }
}
