using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Helpers;
using static TheGuideToTheNewEden.WinUI.Helpers.FindWindowHelper;
using TheGuideToTheNewEden.Core.Extensions;
using Microsoft.UI.Xaml.Media.Imaging;
using SqlSugar.DistributedSystem.Snowflake;
using System.Drawing;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using System.Diagnostics;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class GamePreviewMgrViewModel : BaseViewModel
    {
        private Core.Models.WindowPreviews.PreviewSetting setting;
        public Core.Models.WindowPreviews.PreviewSetting Setting
        {
            get => setting;
            set => SetProperty(ref setting, value);
        }
        private List<WindowInfo> windowInfos;
        public List<WindowInfo> WindowInfos
        {
            get => windowInfos;
            set => SetProperty(ref windowInfos, value);
        }
        private WindowInfo selectedWindowInfo;
        public WindowInfo SelectedWindowInfo
        {
            get => selectedWindowInfo;
            set
            {
                SetProperty(ref selectedWindowInfo, value);
                SelectWindowInfo();
            }
        }
        private ImageSource previewImage;
        public ImageSource PreviewImage
        {
            get => previewImage;
            set => SetProperty(ref previewImage, value);
        }
        private static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "GamePreviewSetting.json");
        public GamePreviewMgrViewModel()
        {
            if(System.IO.File.Exists(Path))
            {
                string json = System.IO.File.ReadAllText(Path);
                if (string.IsNullOrEmpty(json))
                {
                    Setting = new Core.Models.WindowPreviews.PreviewSetting();
                }
                else
                {
                    Setting = JsonConvert.DeserializeObject<Core.Models.WindowPreviews.PreviewSetting>(json);
                }
            }
            else
            {
                Setting = new Core.Models.WindowPreviews.PreviewSetting();
            }
            Init();
        }
        private async void Init()
        {
            Window?.ShowWaiting();
            var list = await FindWindowHelper.EnumWindowsAsync();
            Window?.HideWaiting();
            if (list.NotNullOrEmpty())
            {
                List<WindowInfo> infos = new List<WindowInfo>();
                var keywords = Setting.TitleKeywords.Split(',');
                foreach (var windowInfo in list)
                {
                    foreach(var keyword in keywords)
                    {
                        if(windowInfo.Title.Contains(keyword))
                        {
                            infos.Add(windowInfo);
                            break;
                        }
                    }
                }
                WindowInfos = infos;
            }
        }
        private void SelectWindowInfo()
        {
            if(selectedWindowInfo.hwnd != IntPtr.Zero)
            {
                var img = Helpers.WindowCaptureHelper.GetShotCutImage(selectedWindowInfo.hwnd);
                if (img != null)
                {
                    //string tempFileName = System.IO.Path.GetTempFileName();
                    //img.Save(tempFileName);
                    //UpdatePreviewImage?.Invoke(tempFileName);
                    PreviewImage = Helpers.BitmapConveters.ConvertToBitMapSource(img);
                }
            }
        }
        public delegate void UpdatePreviewImageDelegate(string path);
        public event UpdatePreviewImageDelegate UpdatePreviewImage;

        public ICommand RefreshWindowCommand => new RelayCommand(() =>
        {
            Init();
        });
    }
}
