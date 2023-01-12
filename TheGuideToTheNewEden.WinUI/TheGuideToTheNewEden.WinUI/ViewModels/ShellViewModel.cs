using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Models;
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
                new ToolItem("角色","技能、邮件、合同...", null),
                new ToolItem("市场","实时查看公开市场订单",null),
                new ToolItem("比价","市场价格对比",null),
                new ToolItem("预警","聊天频道手动预警", null)
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
