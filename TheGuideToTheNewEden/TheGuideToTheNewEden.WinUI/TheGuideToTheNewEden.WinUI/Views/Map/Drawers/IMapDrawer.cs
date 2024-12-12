using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Models.Map;

namespace TheGuideToTheNewEden.WinUI.Views.Map.Drawers
{
    public interface IMapDrawer
    {
        /// <summary>
        /// 由MapCanvas发起Drawer绘制
        /// </summary>
        /// <param name="args"></param>
        /// <param name="datas"></param>
        void Draw(CanvasDrawEventArgs args, Dictionary<int, MapData> allDatas, IEnumerable<MapData> visibleDatas);

        /// <summary>
        /// 由Drawer触发MapCanvas绘制
        /// 当Drawer需要更新时使用
        /// </summary>
        event EventHandler DrawRequsted;
    }
}
