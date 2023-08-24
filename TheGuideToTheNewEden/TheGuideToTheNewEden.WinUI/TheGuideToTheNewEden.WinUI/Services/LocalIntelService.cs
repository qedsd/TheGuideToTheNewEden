using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.WinUI.Notifications;

namespace TheGuideToTheNewEden.WinUI.Services
{
    /// <summary>
    /// 用于计算声望变化和发出通知
    /// 每个LocalIntelPage对应一个service
    /// </summary>
    internal class LocalIntelService
    {
        class StandingChange
        {
            public LocalIntelStandingSetting Setting;
            public int Change;
        }
        private readonly List<Tuple<OpenCvSharp.Rect, LocalIntelStandingSetting>> _lastStandings = new List<Tuple<OpenCvSharp.Rect, LocalIntelStandingSetting>>();
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
            var rects = IntelImageHelper.CalStandingRects(edgeMat, 5);
            sender.ChangeGrayImg(edgeMat);
            sender.ChangeStandingRects(sourceMat,rects);
            if (rects.NotNullOrEmpty())
            {
                List<Tuple<OpenCvSharp.Rect, LocalIntelStandingSetting>> matchList = new List<Tuple<OpenCvSharp.Rect, LocalIntelStandingSetting>>();
                OpenCvSharp.Vec3b[] vec3bs = new OpenCvSharp.Vec3b[rects.Count];
                for(int i = 0; i < rects.Count;i++)
                {
                    vec3bs[i] = IntelImageHelper.GetMainColor(rects[i], sourceMat);
                }
                for (int i = 0; i < vec3bs.Length; i++)
                {
                    foreach(var refColors in sender.StandingSettings)
                    {
                        if(IsMatch(refColors.Color, vec3bs[i]))
                        {
                            matchList.Add(new Tuple<OpenCvSharp.Rect, LocalIntelStandingSetting>(rects[i], refColors));
                            break;
                        }
                    }
                }
                if(_lastStandings.Count == matchList.Count)//总匹配的声望和上一回一样，需要排除位置不同
                {
                    for(int i = 0;i< _lastStandings.Count; i++)
                    {
                        var centerY1 = _lastStandings[i].Item1.Top + _lastStandings[i].Item1.Height / 2;
                        var centerY2 = matchList[i].Item1.Top + matchList[i].Item1.Height / 2;
                        if (Math.Abs(centerY1 - centerY2) < _lastStandings[i].Item1.Height * 0.2)//位置误差在高度的20%算相同位置
                        {
                            if(_lastStandings[i].Item2.Equals(matchList[i].Item2))//位置相同颜色也相同
                            {
                                continue;
                            }
                            else//只要出现位置相同颜色不同就是有变化，需要预警
                            {
                                //找出变化
                                List<StandingChange> standingChanges = new List<StandingChange>();
                                var lastGroup = _lastStandings.Skip(i).GroupBy(p => p.Item2);
                                foreach(var group in lastGroup)
                                {
                                    var sameMath = matchList.Where(p => p.Item2.Color == group.Key.Color).ToList();
                                    standingChanges.Add(new StandingChange()
                                    {
                                        Setting = group.Key,
                                        Change = sameMath.Count - group.Count()
                                    });
                                }
                                SendNotify(sender, standingChanges);
                                break;
                            }
                        }
                        else
                        {
                            //位置发生了变化，先检查声望有没有变化，有则提示声望变化，没则提示注意预警
                            //找出变化
                            List<StandingChange> standingChanges = new List<StandingChange>();
                            var lastGroup = _lastStandings.GroupBy(p => p.Item2);
                            foreach (var group in lastGroup)
                            {
                                var sameMath = matchList.Where(p => p.Item2.Color == group.Key.Color).ToList();
                                standingChanges.Add(new StandingChange()
                                {
                                    Setting = group.Key,
                                    Change = sameMath.Count - group.Count()
                                });
                            }
                            if(standingChanges.FirstOrDefault(p=>p.Change !=0) != null)
                            {
                                //有声望变化，提示声望变化
                                SendNotify(sender, standingChanges);
                            }
                            else
                            {
                                //无声望变化，提示注意预警
                                SendNotify(sender, "声望区域定位波动，请注意");
                            }
                            break;
                        }
                    }
                }
                else//总匹配的声望和上回不一样，提示变化
                {
                    if(!(_lastStandings.Count > matchList.Count && !sender.NotifyDecrease))
                    {
                        List<StandingChange> standingChanges = new List<StandingChange>();
                        if(_lastStandings.Count == 0)
                        {
                            var groups = matchList.GroupBy(p=>p.Item2).ToList();
                            foreach(var group in groups)
                            {
                                standingChanges.Add(new StandingChange()
                                {
                                    Setting = group.Key,
                                    Change = group.Count()
                                });
                            }
                        }
                        else
                        {
                            var lastGroup = _lastStandings.GroupBy(p => p.Item2);
                            foreach (var group in lastGroup)
                            {
                                var sameMath = matchList.Where(p => p.Item2.Color == group.Key.Color).ToList();
                                standingChanges.Add(new StandingChange()
                                {
                                    Setting = group.Key,
                                    Change = sameMath.Count - group.Count()
                                });
                            }
                        }
                        SendNotify(sender, standingChanges);
                    }
                }
                _lastStandings.Clear();
                _lastStandings.AddRange(matchList);
            }
        }
        private bool IsMatch(System.Drawing.Color refColor, OpenCvSharp.Vec3b targetColor, float threshold = 0.2f)
        {
            if ((float)Math.Abs(targetColor.Item0 - refColor.R) / (refColor.R == 0 ? 255: refColor.R) < threshold)
            {
                if ((float)Math.Abs(targetColor.Item1 - refColor.G) / (refColor.G == 0 ? 255 : refColor.G) < threshold)
                {
                    if ((float)Math.Abs(targetColor.Item2 - refColor.B) / (refColor.B == 0 ? 255 : refColor.B) < threshold)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        private bool IsMatch(OpenCvSharp.Vec3b refColor, OpenCvSharp.Vec3b targetColor, float threshold = 0.2f)
        {
            if((float)Math.Abs(targetColor.Item0 - refColor.Item0) / (refColor.Item0 == 0 ? 255 : refColor.Item0) < threshold)
            {
                if ((float)Math.Abs(targetColor.Item1 - refColor.Item1) / (refColor.Item1 == 0 ? 255 : refColor.Item1) < threshold)
                {
                    if ((float)Math.Abs(targetColor.Item2 - refColor.Item2) / (refColor.Item2 == 0 ? 255 : refColor.Item2) < threshold)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        #region 通知
        private void SendNotify(LocalIntelProcSetting setting, List<StandingChange> standingChanges)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach(var change in standingChanges)
            {
                if(change.Change != 0)
                {
                    stringBuilder.Append(change.Setting.Name);
                    if(change.Change >0)
                    {
                        stringBuilder.Append('+');
                    }
                    stringBuilder.Append(change.Change);
                }
            }
            SendNotify(setting, stringBuilder.ToString());
        }
        private void SendNotify(LocalIntelProcSetting setting, string msg)
        {
            if(setting.WindowNotify)
                SendWindowNotify(msg);
            if(setting.ToastNotify)
                SendToastNotify(setting.Name, msg);
            if (setting.SoundNotify)
                SendSoundNotify(setting.SoundFile);
        }

        private void SendWindowNotify(string msg)
        {

        }
        private void SendToastNotify(string title, string msg)
        {
            LocalIntelToast.SendToast(title,msg);
        }
        private void SendSoundNotify(string file)
        {

        }
        #endregion
    }
}
