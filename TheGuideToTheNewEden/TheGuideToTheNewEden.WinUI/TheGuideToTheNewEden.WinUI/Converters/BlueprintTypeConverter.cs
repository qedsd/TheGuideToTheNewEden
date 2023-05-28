using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    class BlueprintTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var item = value as Core.Models.Contract.ContractItem;
            if(item != null && item.IsBlueprint)
            {
                if (item.IsBlueprintCopy)
                {
                    return Helpers.ResourcesHelper.GetString("Blueprint_Copy");
                }
                else
                {
                    return Helpers.ResourcesHelper.GetString("Blueprint_Original");
                }
            }
            else
            {
                return string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
