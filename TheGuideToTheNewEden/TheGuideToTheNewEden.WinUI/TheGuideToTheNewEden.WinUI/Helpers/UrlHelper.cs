using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.KB;

namespace TheGuideToTheNewEden.WinUI.Helpers
{
    public static class UrlHelper
    {
        public static void OpenInBrower(string url)
        {
            System.Diagnostics.Process.Start("explorer.exe", url);
        }
    }
}
