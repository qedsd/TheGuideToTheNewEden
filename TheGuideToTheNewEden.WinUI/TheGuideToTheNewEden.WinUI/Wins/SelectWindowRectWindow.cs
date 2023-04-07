using CommunityToolkit.WinUI.Helpers;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Windows.UI.WindowManagement;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class SelectWindowRectWindow: Window
    {
        public SelectWindowRectWindow()
        {
            var present = Helpers.WindowHelper.GetOverlappedPresenter(this);
            var appWindow = Helpers.WindowHelper.GetAppWindow(this);
            //appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            //Helpers.WindowHelper.HideTitleBar(this);
            SetImage();
        }
        private async void SetImage()
        {
            //Graphics g = Graphics.FromImage(CatchBmp);
            //// 把屏幕图片拷贝到我们创建的空白图片 CatchBmp中
            //g.CopyFromScreen(new System.Drawing.Point(0, 0), new System.Drawing.Point(0, 0), CatchBmp.Size);
        }
    }
}
