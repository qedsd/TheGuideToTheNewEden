using System.Globalization;
using System.Windows.Data;
using TheGuideToTheNewEden.Core.Models.GamePreviews;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 进程列表的显示名：优先用已保存配置里的角色名，其次从窗口标题解析，最后回退到标题原文。
/// </summary>
public sealed class ProcessDisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ProcessInfo process)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(process.Setting?.Name))
        {
            return process.Setting.Name;
        }

        return process.GetCharacterName() ?? process.WindowTitle ?? process.ProcessName ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
