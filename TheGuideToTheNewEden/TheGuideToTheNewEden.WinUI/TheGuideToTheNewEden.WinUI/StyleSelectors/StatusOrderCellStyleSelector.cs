using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Syncfusion.UI.Xaml.DataGrid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.StyleSelectors
{
    internal class StatusOrderCellStyleSelector : StyleSelector
    {
        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            var data = item as Core.Models.Market.StatusOrder;
            var mappingName = (container as GridCell).ColumnBase.GridColumn.MappingName;

            if (mappingName == "Normal")
            {
                if(data.Normal)
                {
                    return Helpers.ResourcesHelper.Get("NormalStatusOrderCellStyle") as Style;
                }
                else
                {
                    return Helpers.ResourcesHelper.Get("UnnormalStatusOrderCellStyle") as Style;
                }
            }
            return base.SelectStyleCore(item, container);
        }
    }
}
