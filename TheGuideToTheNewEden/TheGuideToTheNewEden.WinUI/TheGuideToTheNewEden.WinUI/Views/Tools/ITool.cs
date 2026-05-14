using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Views.Tools
{
    public interface ITool
    {
        void GetWindowSize(out int width, out int height);
    }
}
