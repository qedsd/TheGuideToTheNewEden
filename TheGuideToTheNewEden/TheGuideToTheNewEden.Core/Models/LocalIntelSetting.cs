using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models
{
    public class LocalIntelSetting : ObservableObject
    {
        private int refreshSpan = 100;
        /// <summary>
        /// 更新间隔
        /// 单位毫秒
        /// </summary>
        public int RefreshSpan
        {
            get => refreshSpan; set => SetProperty(ref refreshSpan, value);
        }
        public List<LocalIntelProcSetting> ProcSettings { get; set; } = new List<LocalIntelProcSetting>();
    }

    public class LocalIntelProcSetting : ObservableObject
    {
        [JsonIgnore]
        public IntPtr HWnd { get; set; }
        private string name;
        public string Name
        {
            get => name; set => SetProperty(ref name, value);
        }

        private int x;
        public int X
        {
            get => x; set => SetProperty(ref x, value);
        }
        private int y;
        public int Y
        {
            get => y; set => SetProperty(ref y, value);
        }
        private int width;
        public int Width
        {
            get => width; set => SetProperty(ref width, value);
        }
        private int height;
        public int Height
        {
            get => height; set => SetProperty(ref height, value);
        }

        private string notify;
        public string Notify
        {
            get => notify;
            set => SetProperty(ref notify, value);
        }

        private string soundFile;
        public string SoundFile
        {
            get => soundFile;
            set => SetProperty(ref soundFile, value);
        }
        private bool windowNotify;
        public bool WindowNotify
        {
            get => windowNotify;
            set => SetProperty(ref windowNotify, value);
        }
        private bool toastNotify;
        public bool ToastNotify
        {
            get => toastNotify;
            set => SetProperty(ref toastNotify, value);
        }
        private bool soundNotify;
        public bool SoundNotify
        {
            get => soundNotify;
            set => SetProperty(ref soundNotify, value);
        }

        private bool notifyDecrease = true;
        /// <summary>
        /// 是否提示减少的情况
        /// </summary>
        public bool NotifyDecrease
        {
            get => notifyDecrease;
            set => SetProperty(ref notifyDecrease, value);
        }
        private LocalIntelDetectMode detectMode;
        public LocalIntelDetectMode DetectMode
        {
            get => detectMode;
            set => SetProperty(ref detectMode, value);
        }
        public ObservableCollection<LocalIntelStandingSetting> StandingSettings { get; set; } = new ObservableCollection<LocalIntelStandingSetting>();
        public void ChangeScreenshot(Bitmap img)
        {
            OnScreenshotChanged?.Invoke(this,img);
            img.Dispose();
        }
        public delegate void ScreenshotChangedDelegate(LocalIntelProcSetting sender, Bitmap img);
        public event ScreenshotChangedDelegate OnScreenshotChanged;
    }

    public class LocalIntelStandingSetting : ObservableObject
    {
        private Color color = Color.Red;
        public Color Color
        {
            get => color;
            set => SetProperty(ref color, value);
        }

        private string name;
        public string Name
        {
            get => name;
            set => SetProperty(ref name, value);
        }
    }

    public enum LocalIntelDetectMode
    {
        Auto,
        Fix
    }
}
