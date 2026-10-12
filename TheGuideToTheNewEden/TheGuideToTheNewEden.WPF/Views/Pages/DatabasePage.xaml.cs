using System.Collections;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.ViewModels;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 数据库页（替换原 <c>PlaceholderPage</c> 占位）：SDE 静态库浏览/搜索工具。
/// 搜索逻辑全部在 <see cref="DatabasePageViewModel"/>，页面只做绑定、回车转发与动态列的美化。
/// </summary>
public sealed partial class DatabasePage : System.Windows.Controls.Page
{
    private readonly DatabasePageViewModel _viewModel = new();

    public DatabasePage()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    /// <summary>回车立即搜索（绕过防抖）；单行输入框在 KeyDown 里能收到 Enter。</summary>
    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _viewModel.SearchNow();
            e.Handled = true;
        }
    }

    /// <summary>
    /// 自动生成列的美化：跳过非标量列（防御；六种实体全是标量）；列头 PascalCase → 可读标题
    /// （"SolarSystemName" → "Solar System Name"）；文本列套主题前景样式，数值列右对齐。
    /// </summary>
    private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        var type = e.PropertyType;

        if (type != typeof(string))
        {
            if (type.IsArray || typeof(IEnumerable).IsAssignableFrom(type) || !type.IsValueType)
            {
                e.Cancel = true;
                return;
            }
        }

        e.Column.Header = SplitWords(e.PropertyName);

        if (e.Column is DataGridTextColumn textColumn)
        {
            textColumn.ElementStyle = IsNumeric(type)
                ? (Style)FindResource("DbNumberCellStyle")
                : (Style)FindResource("DbCellStyle");
        }
    }

    private static bool IsNumeric(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t == typeof(byte)
               || t == typeof(sbyte)
               || t == typeof(short)
               || t == typeof(ushort)
               || t == typeof(int)
               || t == typeof(uint)
               || t == typeof(long)
               || t == typeof(ulong)
               || t == typeof(float)
               || t == typeof(double)
               || t == typeof(decimal);
    }

    /// <summary>PascalCase 属性名 → 带空格的列头（"TypeID" → "Type ID"，ID 缩写保持大写）。</summary>
    private static string SplitWords(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]) && name[i - 1] != ' ')
            {
                sb.Append(' ');
            }

            sb.Append(c);
        }

        return sb.ToString();
    }
}
