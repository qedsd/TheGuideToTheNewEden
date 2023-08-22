using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.WinUI.Services
{
    /// <summary>
    /// 用于计算声望变化和发出通知
    /// 每个LocalIntelPage对应一个service
    /// </summary>
    internal class LocalIntelService
    {
        public void Add(LocalIntelProcSetting item)
        {
            item.OnScreenshotChanged += Item_OnScreenshotChanged;
        }
        public void Remove(LocalIntelProcSetting item)
        {
            item.OnScreenshotChanged -= Item_OnScreenshotChanged;
        }

        private void Item_OnScreenshotChanged(LocalIntelProcSetting sender, System.Drawing.Bitmap img)
        {
            var sourceMat = IntelImageHelper.BitmapToMat(img);
            var grayMat = IntelImageHelper.GetGray(sourceMat);
            var edgeMat = IntelImageHelper.GetEdge(grayMat);
            var rects = IntelImageHelper.CalStandingRects(edgeMat, 8);
            if(rects.NotNullOrEmpty())
            {
                foreach(var rect in rects)
                {
                    var rgb = IntelImageHelper.GetMainColor(rect, sourceMat);
                }
            }
        }

        #region 通知
        private void SendNotify(string msg, bool window, bool toast)
        {
            if(window)
                SendWindowNotify(msg);
            if(toast)
                SendToastNotify(msg);
        }

        private void SendWindowNotify(string msg)
        {

        }
        private void SendToastNotify(string msg)
        {

        }
        #endregion
    }
}
