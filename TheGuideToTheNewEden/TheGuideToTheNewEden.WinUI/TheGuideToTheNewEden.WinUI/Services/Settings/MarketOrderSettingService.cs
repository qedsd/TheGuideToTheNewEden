using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    internal static class MarketOrderSettingService
    {
        private const string OrderDurationKey = "OrderDuration";
        public static int OrderDurationValue
        {
            get
            {
                if (int.TryParse(SettingService.GetValue(OrderDurationKey), out var result))
                {
                    return result;
                }
                else
                {
                    return 60;
                }
            }
            set => SettingService.SetValue(OrderDurationKey, value.ToString());
        }
        private const string HistoryDurationKey = "HistoryDuration";
        public static int HistoryDurationValue
        {
            get
            {
                if (int.TryParse(SettingService.GetValue(HistoryDurationKey), out var result))
                {
                    return result;
                }
                else
                {
                    return 60;
                }
            }
            set => SettingService.SetValue(HistoryDurationKey, value.ToString());
        }

        public static readonly string StructureOrderFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "StructureOrders");
        public static readonly string RegionOrderFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "RegionOrders");
        public static readonly string HistoryOrderFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "HistoryOrders");
    }
}
