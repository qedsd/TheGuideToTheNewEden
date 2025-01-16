using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TheGuideToTheNewEden.Core.DBModels.IdName;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    internal class IdNameCategoryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return Helpers.ResourcesHelper.GetString($"IdNameCategory_{(CategoryEnum)value}");
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
