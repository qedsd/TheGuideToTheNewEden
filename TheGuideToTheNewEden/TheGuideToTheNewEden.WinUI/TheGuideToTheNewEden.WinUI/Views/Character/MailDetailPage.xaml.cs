using ESI.NET;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.Mail;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml.Media.Imaging;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class MailDetailPage : Page
    {
        private BaseWindow _window;
        private EsiClient _esiClient;
        private Core.Models.Mail.MailDetail _mailDetail;
        public MailDetailPage(EsiClient esiClient, Core.Models.Mail.MailDetail mailDetail)
        {
            _esiClient = esiClient;
            _mailDetail = mailDetail;
            this.InitializeComponent();
            TextBloc_Subject.Text = _mailDetail.Message.Subject;
            Image_From.Source = new BitmapImage(new Uri($"https://imageserver.eveonline.com/{_mailDetail.Header.Category}/{_mailDetail.Message.From}_64.jpg"));
            TextBlock_From.Text = _mailDetail.Header.FromName;
            TextBlock_Date.Text = _mailDetail.DateTime.ToString();
            TextBlock_Labels.Text = _mailDetail.Labels;
            Loaded += MailDetailPage_Loaded;
        }

        private async void MailDetailPage_Loaded(object sender, RoutedEventArgs e)
        {
            await WebView2_Content.EnsureCoreWebView2Async();
            WebView2_Content.NavigateToString(RegexMailBody(_mailDetail.Message.Body));
            _window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
            if(_mailDetail.Message.Recipients.NotNullOrEmpty())
            {
                _=Task.Run(() =>
                {
                    var nameResp =  _esiClient.Universe.Names(_mailDetail.Message.Recipients.Select(p => p.RecipientId).ToList()).Result;
                    if(nameResp != null && nameResp.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        StringBuilder stringBuilder = new StringBuilder();
                        foreach(var name in nameResp.Data)
                        {
                            stringBuilder.Append(name.Name);
                            stringBuilder.Append(';');
                        }
                        if (stringBuilder.Length > 1)
                        {
                            stringBuilder.Remove(stringBuilder.Length - 1, 1);
                            _window.DispatcherQueue.TryEnqueue(() =>
                            {
                                TextBlock_Recipients.Text = stringBuilder.ToString();
                            });
                        }
                    }
                    else
                    {
                        Core.Log.Error(nameResp?.Message);
                    }
                });
            }
        }

        /// <summary>
        /// h5标签规范化
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        private static string RegexMailBody(string body)
        {
            Regex reg = new Regex(" size=\\\"" + "\\d+" + "\\" + "\"");
            string result = reg.Replace(body, "");
            StringBuilder stringBuilder = new StringBuilder(result);
            Regex reg3 = new Regex("color=\\\".{9}\\\"");
            var m = reg3.Match(result);
            System.Drawing.Color color;
            string colorStr;
            while (m.Success)
            {
                int cindex = m.Index + 7;
                color = ColorHx16toRGB(result.Substring(cindex, 9));
                int r = color.R % 255;
                int g = color.G % 255;
                int b = color.B % 255;
                color = System.Drawing.Color.FromArgb(r, g, b);
                colorStr = ColorRGBtoHx16(color);
                for (int i = 1; i < 9; i++)
                {
                    stringBuilder[cindex + i] = colorStr[i];
                }
                m = m.NextMatch();
            }
            return stringBuilder.ToString();
        }
        /// <summary>
        /// [颜色：16进制转成RGB]
        /// </summary>
        /// <param name="strColor">设置16进制颜色 [返回RGB]</param>
        /// <returns></returns>
        public static System.Drawing.Color ColorHx16toRGB(string strHxColor)
        {
            try
            {
                if (strHxColor.Length == 0)
                {//如果为空
                    return System.Drawing.Color.FromArgb(0, 0, 0);//设为黑色
                }
                else
                {//转换颜色
                    return System.Drawing.Color.FromArgb(int.Parse(strHxColor.Substring(7, 2), System.Globalization.NumberStyles.AllowHexSpecifier), int.Parse(strHxColor.Substring(1, 2), System.Globalization.NumberStyles.AllowHexSpecifier), int.Parse(strHxColor.Substring(3, 2), System.Globalization.NumberStyles.AllowHexSpecifier), int.Parse(strHxColor.Substring(5, 2), System.Globalization.NumberStyles.AllowHexSpecifier));
                }
            }
            catch
            {//设为黑色
                return System.Drawing.Color.FromArgb(0, 0, 0);
            }
        }

        /// <summary>
        /// [颜色：RGB转成16进制]
        /// </summary>
        /// <param name="R">红 int</param>
        /// <param name="G">绿 int</param>
        /// <param name="B">蓝 int</param>
        /// <returns></returns>
        public static string ColorRGBtoHx16(System.Drawing.Color color)
        {
            string R = Convert.ToString(color.R, 16);
            if (R.Length == 1)
                R = "0" + R;
            string G = Convert.ToString(color.G, 16);
            if (G.Length == 1)
                G = "0" + G;
            string B = Convert.ToString(color.B, 16);
            if (B.Length == 1)
                B = "0" + B;
            string A = Convert.ToString(color.A, 16);
            if (A.Length == 1)
                A = "0" + A;
            string HexColor = "#" + R + G + B + A;
            return HexColor;
        }
    }
}
