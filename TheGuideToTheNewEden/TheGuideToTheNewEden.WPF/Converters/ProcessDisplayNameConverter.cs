using System.Globalization;
using System.Windows.Data;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 进程列表的显示名（角色名）：优先用已保存配置里的角色名，其次从窗口标题里解析（"EVE - QQQ" → "QQQ"），
/// 最后回退到标题原文。
/// <para>
/// 为什么是 <see cref="IMultiValueConverter"/>：这东西原来用单值绑定 <c>{Binding Converter=…}</c> 绑在
/// **整个 ProcessInfo 对象**上——WPF 对"路径为 ."的绑定只在源对象发出**空名**的 PropertyChanged 时才重新求值，
/// 而我们改的是具体属性（<c>WindowTitle</c> / <c>Setting</c>），于是转换器**永远不会重跑**：
/// 列表里的名字一直停在进程刚被发现时那一刻的值（EVE 客户端刚启动时标题只有 "EVE"，
/// 选中角色后标题才变成 "EVE - 角色名"——名字列就一直显示 "EVE"，手动刷新也不变）。
/// 改成把两个会变的输入（窗口标题、配置里的角色名）分别绑上：任一变化都会重新求值。
/// </para>
/// </summary>
public sealed class ProcessDisplayNameConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // values[0] = WindowTitle，values[1] = Setting?.Name
        var title = values.Length > 0 ? values[0] as string : null;
        var settingName = values.Length > 1 ? values[1] as string : null;

        if (!string.IsNullOrWhiteSpace(settingName))
        {
            return settingName;
        }

        // 与 Core 的 ProcessInfo.GetCharacterName() 同口径：标题里第一个 '-' 之后的部分
        if (!string.IsNullOrWhiteSpace(title))
        {
            var index = title.IndexOf('-');
            if (index > -1)
            {
                var name = title[(index + 1)..].Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }

            return title;
        }

        return string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => targetTypes.Select(_ => Binding.DoNothing).ToArray();
}
