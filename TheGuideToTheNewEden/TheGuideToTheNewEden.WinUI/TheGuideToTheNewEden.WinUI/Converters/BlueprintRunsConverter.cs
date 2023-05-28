using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    class BlueprintRunsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var item = value as Core.Models.Contract.ContractItem;
            if (item != null && item.IsBlueprint)
            {
                if (!item.IsBlueprintCopy)
                {
                    return Helpers.ResourcesHelper.GetString("Blueprint_InfiniteRuns");
                }
                else
                {
                    return item.Runs;
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
