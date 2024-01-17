using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TheGuideToTheNewEden.Core.DBModels.IdName;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    internal class CategoryEnumToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            switch ((CategoryEnum)value)
            {
                case CategoryEnum.Alliance: return Helpers.ResourcesHelper.GetString("CategoryEnum_Alliance");
                case CategoryEnum.Character: return Helpers.ResourcesHelper.GetString("CategoryEnum_Character");
                case CategoryEnum.Constellation: return Helpers.ResourcesHelper.GetString("CategoryEnum_Constellation");
                case CategoryEnum.Corporation: return Helpers.ResourcesHelper.GetString("CategoryEnum_Corporation");
                case CategoryEnum.InventoryType: return Helpers.ResourcesHelper.GetString("CategoryEnum_InventoryType");
                case CategoryEnum.Region: return Helpers.ResourcesHelper.GetString("CategoryEnum_Region");
                case CategoryEnum.SolarSystem: return Helpers.ResourcesHelper.GetString("CategoryEnum_SolarSystem");
                case CategoryEnum.Station: return Helpers.ResourcesHelper.GetString("CategoryEnum_Station");
                case CategoryEnum.Faction: return Helpers.ResourcesHelper.GetString("CategoryEnum_Faction");
                case CategoryEnum.Structure: return Helpers.ResourcesHelper.GetString("CategoryEnum_Structure");
                default: return value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
