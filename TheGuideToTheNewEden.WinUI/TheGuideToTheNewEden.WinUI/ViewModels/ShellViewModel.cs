using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Models;
using TheGuideToTheNewEden.WinUI.Views;
using Windows.ApplicationModel;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class ShellViewModel : ObservableObject
    {
        public string VersionDescription{get;set;}
        public List<ToolItem> ToolItems { get; set; }
        public ShellViewModel()
        {
            VersionDescription = GetVersionDescription();
            ToolItems = new List<ToolItem>()
            {
                new ToolItem("角色","查看角色技能、邮件、合同等信息", typeof(CharacterPage)),
                new ToolItem("市场","实时查看公开市场订单", typeof(MarketPage)),
                new ToolItem("商业","市场价格对比",typeof(BusinessPage)),
                new ToolItem("频道预警","聊天频道手动预警", typeof(EarlyWarningPage)),
                new ToolItem("本地预警","本地频道声望自动识别", typeof(LocalIntelPage)),
                new ToolItem("窗口置顶","多开时游戏窗口置顶辅助", typeof(MutiWindowPage)),
                new ToolItem("设置","", typeof(SettingPage)),
            };
        }
        private static string GetVersionDescription()
        {
            var package = Package.Current;
            var packageId = package.Id;
            var version = packageId.Version;

            return $"V{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}
