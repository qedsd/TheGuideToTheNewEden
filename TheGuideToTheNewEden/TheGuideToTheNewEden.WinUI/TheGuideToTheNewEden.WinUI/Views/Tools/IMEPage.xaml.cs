using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using Dm.util;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.Core.DBModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.Universe;
using TheGuideToTheNewEden.Core.Models.Translation;
using Windows.ApplicationModel.DataTransfer;
using System.Text;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Text;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using ESI.NET.Models.PlanetaryInteraction;
using TheGuideToTheNewEden.Core.Interfaces;


namespace TheGuideToTheNewEden.WinUI.Views.Tools
{
    public sealed partial class IMEPage : Page, IPage
    {
        public IMEPage()
        {
            this.InitializeComponent();
        }

        public void Close()
        {
            
        }

        /// <summary>
        /// 纯数字
        /// </summary>
        private static readonly Regex PureDigitsRegex = new Regex(@"^\d+$");

        /// <summary>
        /// 包含可选正负号的整数
        /// </summary>
        private static readonly Regex IntegerRegex = new Regex(@"^[+-]?\d+$");

        /// <summary>
        /// 小数（包含正负号）
        /// </summary>
        private static readonly Regex DecimalRegex = new Regex(@"^[+-]?\d*\.?\d+$");

        /// <summary>
        /// 严格的小数（必须包含小数点）
        /// </summary>
        private static readonly Regex StrictDecimalRegex = new Regex(@"^[+-]?\d+\.\d+$");

        /// <summary>
        /// 英文分隔符
        /// </summary>
        private static readonly Regex SeparatorRegex = new Regex(@"^[\p{P}\p{S}\s]$",RegexOptions.Compiled);

        /// <summary>
        /// 中文分隔符
        /// </summary>
        private static readonly Regex ChineseSeparatorRegex = new Regex(@"^[\u3000-\u303F\uFF00-\uFFEF\u2014\u2026\u00B7\u30FB]$",RegexOptions.Compiled);

        /// <summary>
        /// 中文字符
        /// </summary>
        private static readonly Regex ContainsChineseRegex = new Regex(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

        public static bool IsSeparator(char c)
        {
            return SeparatorRegex.IsMatch(c.ToString()) || ChineseSeparatorRegex.IsMatch(c.ToString());
        }
        public static bool IsNumber(string text)
        {
            return PureDigitsRegex.IsMatch(text) || IntegerRegex.IsMatch(text) || DecimalRegex.IsMatch(text) || StrictDecimalRegex.IsMatch(text);
        }
        public static bool IsZh(string text)
        {
            return ContainsChineseRegex.IsMatch(text);
        }
        private bool GetSearchingPart(out string targetText, out int startIndex, out int endIndex)
        {
            targetText = null;
            startIndex = -1;
            endIndex = -1;
            string text = InputTextBox.Text;
            if(string.IsNullOrEmpty(text))
            {
                return false;
            }
            int index  = InputTextBox.SelectionStart - 1;
            if (IsSeparator(text[index]))
            {
                return false;
            }
            else
            {
                startIndex = 0;
                endIndex = text.Length -1;
                for (int i = index; i >= 0; i--)//往前找单词起始索引
                {
                    if (IsSeparator(text[i]))
                    {
                        startIndex = i + 1;
                        break;
                    }
                }
                for (int i = index; i < text.Length; i++)//往后找单词结束索引
                {
                    if (IsSeparator(text[i]))
                    {
                        endIndex = i - 1;
                        break;
                    }
                }
                targetText = text.Substring(startIndex, endIndex - startIndex + 1);
                return !IsNumber(targetText);
            }
        }

        private CancellationTokenSource _cancellationTokenSource;
        private int _startIndex = -1;
        private int _endIndex = -1;
        private async void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_stopTextChanged)
            {
                _stopTextChanged = false;
                return;
            }
            _cancellationTokenSource?.Cancel();
            if (GetSearchingPart(out var targetText, out _startIndex, out _endIndex))
            {
                CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                _cancellationTokenSource = cancellationTokenSource;
                SuggestionsContainer.ItemsSource = await Task.Run(() =>
                {
                    return Serach(targetText, cancellationTokenSource.Token)?.Take(6);
                }); ;
            }
            else
            {
                _startIndex = -1;
                _endIndex = -1;
                SuggestionsContainer.ItemsSource = null;
            }
        }
        private bool _stopTextChanged = false;
        private void SuggestionButton_Click(object sender, RoutedEventArgs e)
        {
            IdName item = (sender as FrameworkElement).DataContext as IdName;
            if(item != null &&_startIndex != -1 && _endIndex != -1)
            {
                _stopTextChanged = true;
                string start = _startIndex > 0 ? InputTextBox.Text.Substring(0, _startIndex) : string.Empty;
                string end = InputTextBox.Text.Substring(_endIndex + 1);
                InputTextBox.Text = $"{start}{item.Name}{end}";
            }
        }
        private List<IdName> Serach(string searchName, CancellationToken cancellationToken)
        {
            List<IdName> result = new List<IdName>();
            var names = Core.Services.IDNameService.SerachByName(searchName);
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            var types = Core.Services.DB.InvTypeService.Search(searchName);
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            var groups = Core.Services.DB.InvGroupService.Search(searchName);
            var systems = Core.Services.DB.MapSolarSystemService.Search(searchName);
            var regions = Core.Services.DB.MapRegionService.Search(searchName);
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            if (types.NotNullOrEmpty())
            {
                foreach (var type in types)
                {
                    result.Add(new IdName()
                    {
                        Id = type.ID,
                        Name = type.Name,
                        Category = (int)IdName.CategoryEnum.InventoryType
                    });
                }
            }
            if (groups.NotNullOrEmpty())
            {
                foreach (var group in groups)
                {
                    result.Add(new IdName()
                    {
                        Id = group.ID,
                        Name = group.Name,
                        Category = (int)IdName.CategoryEnum.Group
                    });
                }
            }
            if (systems.NotNullOrEmpty())
            {
                foreach (var system in systems)
                {
                    result.Add(new IdName()
                    {
                        Id = system.ID,
                        Name = system.Name,
                        Category = (int)IdName.CategoryEnum.SolarSystem
                    });
                }
            }
            if (regions.NotNullOrEmpty())
            {
                foreach (var region in regions)
                {
                    result.Add(new IdName()
                    {
                        Id = region.ID,
                        Name = region.Name,
                        Category = (int)IdName.CategoryEnum.Region
                    });
                }
            }
            if (names.NotNullOrEmpty())
            {
                var hashSet = result.Select(p => p.Id).ToHashSet2();
                foreach (var name in names)
                {
                    if (!hashSet.Contains(name.Id))
                    {
                        hashSet.Add(name.Id);
                        result.Add(name);
                    }
                }
            }
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            return result.OrderBy(p=>p.Name.Length).ToList();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            DataPackage dataPackage = new DataPackage();
            dataPackage.SetText(InputTextBox.Text);
            Clipboard.SetContent(dataPackage);
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            _stopTextChanged = true;
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource = cancellationTokenSource;
            string text = InputTextBox.Text;
            if (InputTextBox.SelectionLength > 0)
            {
                text = text.Substring(InputTextBox.SelectionStart, InputTextBox.SelectionLength);
            }
            var items = await Task.Run(() =>
            {
                return Serach(text, cancellationTokenSource.Token);
            });
            if (items.NotNullOrEmpty())
            {
                List<Paragraph> paragraphs = new List<Paragraph>();
                foreach (var item in items.Take(100))
                {
                    Paragraph paragraph = new Paragraph()
                    {
                        Margin = new Thickness(0, 0, 0, 8),
                    };
                    paragraph.Inlines.Add(new Run()
                    {
                        Text = $"{item.Name}    ({item.GetCategory()} {item.Id})"
                    });
                    paragraphs.add(paragraph);
                }
                ShowMore($"{Helpers.ResourcesHelper.GetString("IMEPage_Serach")}: {text}", paragraphs);
            }
            else
            {
                List<Paragraph> paragraphs = new List<Paragraph>();
                Paragraph paragraph = new Paragraph()
                {
                    Margin = new Thickness(0, 0, 0, 8),
                };
                paragraph.Inlines.Add(new Run()
                {
                    Text = Helpers.ResourcesHelper.GetString("IMEPage_NoResult")
                });
                paragraphs.add(paragraph);
                ShowMore($"{Helpers.ResourcesHelper.GetString("IMEPage_SerachTitle")}{text}", paragraphs);
            }
        }

        private async void TranslateButton_Click(object sender, RoutedEventArgs e)
        {
            _stopTextChanged = true;
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource = cancellationTokenSource;
            string text = InputTextBox.Text;
            if (InputTextBox.SelectionLength > 0)
            {
                text = text.Substring(InputTextBox.SelectionStart, InputTextBox.SelectionLength);
            }

            var result = await ClientServiceHelper.GetRequiredService<ITranslationService>().Translate(text, "auto", IsZh(text) ? "en" : "zh-CHS");
            if (result != null)
            {
                List<Paragraph> paragraphs = new List<Paragraph>();
                Paragraph paragraph = new Paragraph()
                {
                    Margin = new Thickness(0, 0, 0, 8),
                };
                paragraph.Inlines.Add(new Run()
                {
                    Text = result.Result
                });
                paragraphs.add(paragraph);
                ShowMore($"{Helpers.ResourcesHelper.GetString("IMEPage_Serach")}: {text}", paragraphs);
            }
            else
            {
                List<Paragraph> paragraphs = new List<Paragraph>();
                Paragraph paragraph = new Paragraph()
                {
                    Margin = new Thickness(0, 0, 0, 8),
                };
                paragraph.Inlines.Add(new Run()
                {
                    Text = Helpers.ResourcesHelper.GetString("IMEPage_NoResult")
                });
                paragraphs.add(paragraph);
                ShowMore($"{Helpers.ResourcesHelper.GetString("IMEPage_SerachTitle")}{text}", paragraphs);
            }
        }

        private void ExitDetailButton_Click(object sender, RoutedEventArgs e)
        {
            InputTextBox.Visibility = Visibility.Visible;
            InputOpGrid.Visibility = Visibility.Visible;
            DetailGrid.Visibility = Visibility.Collapsed;
            DetailOpGrid.Visibility = Visibility.Collapsed;
        }
        private void ShowMore(string title, List<Paragraph> contents)
        {
            InputTextBox.Visibility = Visibility.Collapsed;
            InputOpGrid.Visibility = Visibility.Collapsed;
            DetailGrid.Visibility = Visibility.Visible;
            DetailOpGrid.Visibility = Visibility.Visible;
            DetailTitle.Text = title;
            DetailContent.Blocks.Clear();
            foreach (var item in contents)
            {
                DetailContent.Blocks.Add(item);
            }
        }
    }
}
