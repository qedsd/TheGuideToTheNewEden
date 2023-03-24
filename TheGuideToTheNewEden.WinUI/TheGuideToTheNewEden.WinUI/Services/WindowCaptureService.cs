using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TheGuideToTheNewEden.WinUI.Helpers.FindWindowHelper;

namespace TheGuideToTheNewEden.WinUI.Services
{
    internal class WindowCaptureService
    {
        private WindowCaptureService current;
        public WindowCaptureService Current
        {
            get
            {
                if(current == null)
                {
                    current = new WindowCaptureService();
                }
                return current;
            }
        }

    }
}
