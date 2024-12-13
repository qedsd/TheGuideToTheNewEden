using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Models.Map;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using TheGuideToTheNewEden.Core.Extensions;
using ESI.NET.Models.Universe;
using Microsoft.UI.Xaml;
using Microsoft.Graphics.Canvas.Geometry;

namespace TheGuideToTheNewEden.WinUI.Views.Map.Drawers
{
    public class JumpBridgeDrawer: IMapDrawer
    {
        private bool _isDark = false;
        private Windows.UI.Color _linkColor;
        private CanvasStrokeStyle _lineStyle;
        public JumpBridgeDrawer()
        {
            SetColor(Services.Settings.ThemeSelectorService.IsDark);
            JumpBridgeSetting.SettingChanged += JumpBridgeSetting_SettingChanged;
            Services.Settings.ThemeSelectorService.OnChangedTheme += ThemeSelectorService_OnChangedTheme;
            _lineStyle = new CanvasStrokeStyle()
            {
                DashStyle = CanvasDashStyle.Dot
            };
        }
        private void ThemeSelectorService_OnChangedTheme(ElementTheme theme)
        {
            _isDark = Services.Settings.ThemeSelectorService.IsDark;
            SetColor(_isDark);
            DrawRequsted?.Invoke(this, EventArgs.Empty);
        }
        private void SetColor(bool isDark)
        {
            _linkColor = isDark ?
                         Windows.UI.Color.FromArgb(255, Microsoft.UI.Colors.LightGray.R, Microsoft.UI.Colors.LightGray.G, Microsoft.UI.Colors.LightGray.B) :
                         Windows.UI.Color.FromArgb(255, Microsoft.UI.Colors.DarkGray.R, Microsoft.UI.Colors.DarkGray.G, Microsoft.UI.Colors.DarkGray.B);
        }
        public event EventHandler DrawRequsted;

        private void JumpBridgeSetting_SettingChanged(object sender, EventArgs e)
        {
            DrawRequsted?.Invoke(this, EventArgs.Empty);
        }

        public void Draw(CanvasDrawEventArgs args, Dictionary<int, MapData> allDatas, IEnumerable<MapData> visibleDatas)
        {
            if(JumpBridgeSetting.IsShowBridge() && JumpBridgeSetting.ExistBridge())
            {
                HashSet<int> drew = new HashSet<int>();//已绘制的星系id
                foreach (var data in visibleDatas)
                {
                    if(JumpBridgeSetting.GetValue(data.Id, out int toSystemID))
                    {
                        if(allDatas.TryGetValue(toSystemID, out var linkToData))
                        {
                            var ponit0 = new System.Numerics.Vector2((float)data.CenterX, (float)data.CenterY);
                            var ponit1 = new System.Numerics.Vector2((float)linkToData.CenterX, (float)linkToData.CenterY);
                            args.DrawingSession.DrawLine(ponit0, ponit1, _linkColor, 2, _lineStyle);
                        }
                    }
                }
            }
        }
    }
}
