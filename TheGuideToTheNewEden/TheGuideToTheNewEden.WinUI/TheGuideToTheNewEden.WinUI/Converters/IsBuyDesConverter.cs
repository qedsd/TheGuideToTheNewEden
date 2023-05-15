using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    internal class IsBuyDesConverter : IValueConverter
    {
        private static string _buy;
        private static string _sell;
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if((bool)value)
            {
                if(string.IsNullOrEmpty(_buy))
                {
                    if (Application.Current.Resources.TryGetValue("WalletPage_TransactionBuy", out var str))
                    {
                        _buy = (string)str;
                    }
                    else
                    {
                        _buy = "Buy";
                    }
                }
                return _buy;
            }
            else
            {
                if (string.IsNullOrEmpty(_sell))
                {
                    if (Application.Current.Resources.TryGetValue("WalletPage_TransactionSell", out var str))
                    {
                        _sell = (string)str;
                    }
                    else
                    {
                        _sell = "Sell";
                    }
                }
                return _sell;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
