using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Syncfusion.UI.Xaml.DataGrid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.PlanetResources;
using static TheGuideToTheNewEden.WinUI.Dialogs.MapSystemDetailDialog;

namespace TheGuideToTheNewEden.WinUI.StyleSelectors
{
    internal class UpgradePowerMeetCellStyle : StyleSelector
    {
        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            var data = item as UpgradeStatus;
            var mappingName = (container as GridCell).ColumnBase.GridColumn.MappingName;

            if (mappingName == "Upgrade.Name")
            {
                if (data.Fit)
                {
                    return Helpers.ResourcesHelper.Get("GreenCellStyle") as Style;
                }
                else
                {
                    return Helpers.ResourcesHelper.Get("RedCellStyle") as Style;
                }
            }
            return base.SelectStyleCore(item, container);
        }
    }
}
