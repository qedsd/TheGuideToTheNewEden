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
        public bool Enable { get; set; } = true;
        public event EventHandler DrawRequsted;
        public event EventHandler<string> OnError;

        public void RequstDraw()
        {
            DrawRequsted?.Invoke(this, EventArgs.Empty);
        }
        public void Error(string error)
        {
            OnError?.Invoke(this, error);
        }
        public abstract void Draw(CanvasDrawEventArgs args, Dictionary<int, MapData> allDatas, IEnumerable<MapData> visibleDatas, float zoom, bool drawBorder, Windows.UI.Color mainTextColor);
        public abstract void Close();
        public virtual void Stop()
        {

        }

        public virtual void Start()
        {

        }

        public virtual bool GetEnable()
        {
            return Enable;
        }

        public virtual void SetEnable(bool enable)
        {
            Enable = enable;
        }
    }
}
