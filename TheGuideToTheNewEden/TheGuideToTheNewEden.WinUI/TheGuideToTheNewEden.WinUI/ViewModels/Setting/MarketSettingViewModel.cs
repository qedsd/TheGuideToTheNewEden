using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Services.Settings;

namespace TheGuideToTheNewEden.WinUI.ViewModels.Setting
{
    public class MarketSettingViewModel : BaseViewModel
    {
        private double orderDuration = MarketOrderSettingService.OrderDurationValue;
        public double OrderDuration
        {
            get => orderDuration;
            set
            {
                if(SetProperty(ref orderDuration, value))
                {
                    MarketOrderSettingService.OrderDurationValue = (int)value;
                }
            }
        }
        private double historyDuration = MarketOrderSettingService.HistoryDurationValue;
        public double HistoryDuration
        {
            get => historyDuration;
            set
            {
                if (SetProperty(ref historyDuration, value))
                {
                    MarketOrderSettingService.HistoryDurationValue = (int)value;
                }
            }
        }

        public ICommand ClearCacheCommand => new RelayCommand(() =>
        {
            if(System.IO.Directory.Exists(MarketOrderSettingService.StructureOrderFolder))
                System.IO.Directory.Delete(MarketOrderSettingService.StructureOrderFolder, true);

            if (System.IO.Directory.Exists(MarketOrderSettingService.RegionOrderFolder))
                System.IO.Directory.Delete(MarketOrderSettingService.RegionOrderFolder, true);

            if (System.IO.Directory.Exists(MarketOrderSettingService.HistoryOrderFolder))
                System.IO.Directory.Delete(MarketOrderSettingService.HistoryOrderFolder, true);
            Window?.ShowSuccess("已清除缓存订单信息");
        });
    }
}
