using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Models.Map;

namespace TheGuideToTheNewEden.WinUI.Views.Map.Drawers
{
    public abstract class MapDrawerBase : IMapDrawer
    {
        public event EventHandler DrawRequsted;

        public abstract void Draw(CanvasDrawEventArgs args, Dictionary<int, MapData> allDatas, IEnumerable<MapData> visibleDatas);
        public void RequstDraw()
        {
            DrawRequsted?.Invoke(this, EventArgs.Empty);
        }
    }
}
