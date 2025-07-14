using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WPF.Views
{
    internal interface IPage
    {
        void Init();
        /// <summary>
        /// 关闭页面
        /// </summary>
        void Close();
    }
}
