﻿using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Helpers;
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
                new ToolItem("市场","查看公开市场订单", typeof(MarketPage)),
                new ToolItem("商业","市场价格对比",typeof(BusinessPage)),
                new ToolItem("频道预警","聊天频道手动预警", typeof(EarlyWarningPage)),
                //new ToolItem("本地预警","本地频道声望自动识别", typeof(LocalIntelPage)),
                new ToolItem("多开预览","多开时游戏窗口置顶辅助", typeof(GamePreviewMgrPage)),
                new ToolItem("公开合同","查看星系的公开合同", typeof(ContractPage)),
                new ToolItem("延迟测试","测试本地与游戏服务器直接延迟", typeof(ServerPingPage)),
                new ToolItem("翻译","查看游戏专有名词中英互译", typeof(TranslationPage)),
                new ToolItem("死亡远征","死亡远征攻略", typeof(DEDPage)),
                new ToolItem("设置","", typeof(SettingPage)),
            };
        }
        private static string GetVersionDescription()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }
    }
}
