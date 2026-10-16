using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// 极简 **Markdown → <see cref="FlowDocument"/>** 渲染器，供 DED / 任务攻略这类"库里的 wiki 正文"使用。
/// <para>
/// 为什么自己写：WinUI 版用 CommunityToolkit 的 <c>MarkdownTextBlock</c>，WPF 没有对应控件；
/// 而本项目既有的依赖里没有 Markdown 库（<c>HtmlRenderer.WPF</c> 只吃 HTML，引 Markdig 又要新增包）。
/// 库里的正文只用到一小撮语法，因此按实测覆盖面实现，**不引入任何新依赖**。
/// </para>
/// <para>
/// 覆盖范围（按 <c>ded.db</c> / <c>static.db</c> 的真实内容统计得出）：
/// 标题（<c>#</c>~<c>######</c>，允许 <c>###文字</c> 这种没空格的写法）、段落、粗体、斜体、删除线、
/// 行内代码、链接、图片（降级成链接）、无序/有序列表（支持缩进嵌套）、引用块、水平线、
/// 围栏代码块、管道表格（含表头与 <c>:---:</c> 对齐）、<c>&lt;br&gt;</c> 与 HTML 标签/实体。
/// </para>
/// <para>
/// 已知取舍：正文里的**换行按原样保留**（单换行渲染成 <see cref="LineBreak"/>）——库里的内容是从 wiki
/// 导出的"行式"文本，按 Markdown 标准把单换行当空格会把可读性弄得更差；图片不下载（离线友好），
/// 渲染成带 alt 文本的链接，点击在浏览器打开。
/// </para>
/// </summary>
public static class MarkdownRenderer
{
    // ---------- 块级语法 ----------

    /// <summary>
    /// 说明书内链地址（<c>[邮件](characters/mail)</c> 这类）：没有 scheme、不以 <c>/</c> 开头、
    /// 只由 id 字符（字母数字 _ - /）与可选 <c>#节锚点</c> 组成。命中即视为手册 id，转 <c>manual:</c> 协议。
    /// </summary>
    private static readonly Regex ManualLinkRegex = new(
        @"^[A-Za-z0-9][A-Za-z0-9_-]*(?:/[A-Za-z0-9_-]+)*(?:#[A-Za-z0-9_-]+)?$",
        RegexOptions.Compiled);

    private static readonly Regex HeadingRegex = new(
        @"^\s{0,3}(?<hashes>#{1,6})\s*(?<text>.*?)\s*#*\s*$",
        RegexOptions.Compiled);

    private static readonly Regex FenceRegex = new(
        @"^\s{0,3}(?<fence>```+|~~~+)\s*(?<lang>[^\s]*)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex UnorderedListRegex = new(
        @"^(?<indent>[ \t]*)(?<marker>[-*+])(?<space>\s+)(?<text>.*)$",
        RegexOptions.Compiled);

    private static readonly Regex OrderedListRegex = new(
        @"^(?<indent>[ \t]*)(?<marker>\d{1,9})[.)](?<space>\s+)(?<text>.*)$",
        RegexOptions.Compiled);

    private static readonly Regex QuoteRegex = new(
        @"^\s{0,3}>\s?(?<text>.*)$",
        RegexOptions.Compiled);

    private static readonly Regex HorizontalRuleRegex = new(
        @"^\s{0,3}(?<ch>[-*_])[ \t]*(?:\k<ch>[ \t]*){2,}$",
        RegexOptions.Compiled);

    // ---------- 行内语法 ----------
    // 注意替代顺序：先匹配长标记（** 在 * 之前、![ 在 [ 之前），否则会被短标记先吃掉。

    private static readonly Regex InlineRegex = new(
        @"(?<code>`[^`\r\n]*`)" +
        @"|(?<image>!\[(?<imgalt>[^\]\r\n]*)\]\(\s*(?<imgurl>[^)\s]+)(?:\s+""[^""]*"")?\s*\))" +
        @"|(?<link>\[(?<linktext>[^\]\r\n]*)\]\(\s*(?<linkurl>[^)\s]+)(?:\s+""[^""]*"")?\s*\))" +
        @"|(?<bold>\*\*(?<boldtext>[^*\r\n]+)\*\*)" +
        @"|(?<bold2>__(?<boldtext2>[^_\r\n]+)__)" +
        @"|(?<strike>~~(?<striketext>[^~\r\n]+)~~)" +
        @"|(?<italic>\*(?<italictext>[^*\r\n]+)\*)" +
        @"|(?<italic2>_(?<italictext2>[^_\r\n]+)_)" +
        @"|(?<br><[bB][rR]\s*/?>)" +
        @"|(?<tag></?[a-zA-Z][^>\r\n]{0,300}>)" +
        @"|(?<entity>&(?:[a-zA-Z]{2,8}|#\d{1,6});)",
        RegexOptions.Compiled);

    private static readonly FontFamily MonoFont = new("Consolas, Microsoft YaHei UI, monospace");

    /// <summary>把 Markdown 文本渲染成可选中复制的 <see cref="FlowDocument"/>。</summary>
    public static FlowDocument ToFlowDocument(string? markdown)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            LineHeight = double.NaN,
        };

        var lines = SplitLines(markdown);
        var index = 0;

        while (index < lines.Length)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                index++;
                continue;
            }

            if (FenceRegex.IsMatch(line))
            {
                index = AddCodeBlock(lines, index, document);
                continue;
            }

            if (HeadingRegex.Match(line) is { Success: true } heading)
            {
                document.Blocks.Add(BuildHeading(heading.Groups["hashes"].Length, heading.Groups["text"].Value));
                index++;
                continue;
            }

            // 表格：当前行有 |，且下一行是分隔行（`| --- | --- |`）。放在水平线之前判断，
            // 因为分隔行本身长得像水平线。
            if (line.Contains('|') && index + 1 < lines.Length && IsTableSeparator(lines[index + 1]))
            {
                index = AddTable(lines, index, document);
                continue;
            }

            if (HorizontalRuleRegex.IsMatch(line))
            {
                document.Blocks.Add(BuildHorizontalRule());
                index++;
                continue;
            }

            if (QuoteRegex.IsMatch(line))
            {
                index = AddBlockQuote(lines, index, document);
                continue;
            }

            if (UnorderedListRegex.IsMatch(line) || OrderedListRegex.IsMatch(line))
            {
                index = AddList(lines, index, document);
                continue;
            }

            index = AddParagraph(lines, index, document);
        }

        if (document.Blocks.Count == 0)
        {
            // 空文档也留一个空段落，避免 RichTextBox 里出现"没有任何块"的怪异状态。
            document.Blocks.Add(new Paragraph { Margin = new Thickness(0) });
        }

        return document;
    }

    // ---------- 块级实现 ----------

    private static int AddCodeBlock(string[] lines, int index, FlowDocument document)
    {
        var fence = FenceRegex.Match(lines[index]).Groups["fence"].Value;
        var marker = fence[0];
        var builder = new StringBuilder();
        index++;

        while (index < lines.Length)
        {
            var candidate = lines[index].Trim();
            if (candidate.Length >= 3 && candidate[0] == marker
                && candidate.TrimStart(marker) is { Length: 0 })
            {
                index++;
                break;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(lines[index]);
            index++;
        }

        var paragraph = new Paragraph(new Run(builder.ToString()))
        {
            FontFamily = MonoFont,
            FontSize = 13,
            Margin = new Thickness(0, 4, 0, 12),
            Padding = new Thickness(10, 8, 10, 8),
        };
        paragraph.SetResourceReference(TextElement.BackgroundProperty, "ControlFillColorSecondaryBrush");
        document.Blocks.Add(paragraph);
        return index;
    }

    private static Paragraph BuildHeading(int level, string text)
    {
        var paragraph = new Paragraph
        {
            FontSize = level switch
            {
                1 => 24,
                2 => 20,
                3 => 18,
                4 => 16,
                5 => 15,
                _ => 14,
            },
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, level <= 2 ? 14 : 10, 0, 6),
        };

        AppendInlines(paragraph.Inlines, text);
        if (paragraph.Inlines.Count == 0)
        {
            paragraph.Inlines.Add(new Run(text));
        }

        return paragraph;
    }

    private static Paragraph BuildHorizontalRule()
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 8, 0, 12),
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0, 1, 0, 0),
        };
        paragraph.SetResourceReference(Block.BorderBrushProperty, "CardStrokeColorDefaultBrush");
        return paragraph;
    }

    private static int AddBlockQuote(string[] lines, int index, FlowDocument document)
    {
        var parts = new List<string>();
        while (index < lines.Length && QuoteRegex.Match(lines[index]) is { Success: true } quote)
        {
            parts.Add(quote.Groups["text"].Value);
            index++;
        }

        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 2, 0, 12),
            Padding = new Thickness(12, 2, 0, 2),
            BorderThickness = new Thickness(3, 0, 0, 0),
        };
        paragraph.SetResourceReference(Block.BorderBrushProperty, "SystemAccentColorPrimaryBrush");
        paragraph.SetResourceReference(TextElement.ForegroundProperty, "TextFillColorSecondaryBrush");

        for (var i = 0; i < parts.Count; i++)
        {
            if (i > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }

            AppendInlines(paragraph.Inlines, parts[i]);
        }

        document.Blocks.Add(paragraph);
        return index;
    }

    private static int AddParagraph(string[] lines, int index, FlowDocument document)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 10) };

        while (index < lines.Length
               && !string.IsNullOrWhiteSpace(lines[index])
               && !StartsNewBlock(lines, index, isFirstLine: paragraph.Inlines.Count == 0))
        {
            if (paragraph.Inlines.Count > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }

            AppendInlines(paragraph.Inlines, lines[index].Trim());
            index++;
        }

        document.Blocks.Add(paragraph);
        return index;
    }

    /// <summary>当前行是否会开启一个新的块（用于决定段落在哪里断开）。</summary>
    private static bool StartsNewBlock(string[] lines, int index, bool isFirstLine)
    {
        if (isFirstLine)
        {
            return false;
        }

        var line = lines[index];
        if (string.IsNullOrWhiteSpace(line))
        {
            return true;
        }

        if (FenceRegex.IsMatch(line)
            || HeadingRegex.IsMatch(line)
            || HorizontalRuleRegex.IsMatch(line)
            || QuoteRegex.IsMatch(line)
            || UnorderedListRegex.IsMatch(line)
            || OrderedListRegex.IsMatch(line))
        {
            return true;
        }

        return line.Contains('|') && index + 1 < lines.Length && IsTableSeparator(lines[index + 1]);
    }

    private static int AddList(string[] lines, int index, FlowDocument document)
    {
        // (缩进, 该层的 List, 该层最近一个 ListItem)——嵌套列表要挂到父项里，所以得记住"父级最后一个项"。
        var levels = new List<(int Indent, List List, ListItem? Last)>();

        while (index < lines.Length)
        {
            var unordered = UnorderedListRegex.Match(lines[index]);
            var ordered = OrderedListRegex.Match(lines[index]);

            bool isOrdered;
            string indentText;
            string text;

            if (unordered.Success)
            {
                isOrdered = false;
                indentText = unordered.Groups["indent"].Value;
                text = unordered.Groups["text"].Value;
            }
            else if (ordered.Success)
            {
                isOrdered = true;
                indentText = ordered.Groups["indent"].Value;
                text = ordered.Groups["text"].Value;
            }
            else
            {
                break;
            }

            var indent = IndentWidth(indentText);

            // 回到与当前缩进匹配的那一层
            while (levels.Count > 0 && indent < levels[^1].Indent)
            {
                levels.RemoveAt(levels.Count - 1);
            }

            if (levels.Count == 0)
            {
                var root = NewList(isOrdered);
                document.Blocks.Add(root);
                levels.Add((indent, root, null));
            }
            else if (indent > levels[^1].Indent)
            {
                var parent = levels[^1];
                var child = NewList(isOrdered);
                if (parent.Last is null)
                {
                    // 理论上不会发生（上一行必然建立了 ListItem），兜底用一个空项承载子列表。
                    var host = new ListItem(new Paragraph());
                    host.Blocks.Add(child);
                    parent.List.ListItems.Add(host);
                }
                else
                {
                    parent.Last.Blocks.Add(child);
                }

                levels.Add((indent, child, null));
            }

            var item = new ListItem(BuildListItemParagraph(text));
            levels[^1].List.ListItems.Add(item);
            levels[^1] = (levels[^1].Indent, levels[^1].List, item);
            index++;
        }

        return index;
    }

    private static List NewList(bool ordered) => new()
    {
        MarkerStyle = ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
        Margin = new Thickness(0, 2, 0, 10),
        Padding = new Thickness(22, 0, 0, 0),
    };

    private static Paragraph BuildListItemParagraph(string text)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 2) };
        AppendInlines(paragraph.Inlines, text);
        return paragraph;
    }

    private static int AddTable(string[] lines, int index, FlowDocument document)
    {
        var header = SplitTableRow(lines[index]);
        if (header.Count == 0)
        {
            return AddParagraph(lines, index, document);
        }

        var alignments = ParseAlignments(lines[index + 1], header.Count);
        index += 2;

        var rows = new List<List<string>>();
        while (index < lines.Length
               && !string.IsNullOrWhiteSpace(lines[index])
               && lines[index].Contains('|'))
        {
            rows.Add(SplitTableRow(lines[index]));
            index++;
        }

        var table = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 4, 0, 12),
        };

        for (var c = 0; c < header.Count; c++)
        {
            table.Columns.Add(new TableColumn());
        }

        var group = new TableRowGroup();
        group.Rows.Add(BuildTableRow(header, alignments, isHeader: true));
        foreach (var row in rows)
        {
            group.Rows.Add(BuildTableRow(row, alignments, isHeader: false));
        }

        table.RowGroups.Add(group);
        document.Blocks.Add(table);
        return index;
    }

    private static TableRow BuildTableRow(List<string> cells, List<TextAlignment> alignments, bool isHeader)
    {
        var row = new TableRow();

        for (var c = 0; c < cells.Count; c++)
        {
            var cell = new TableCell
            {
                Padding = new Thickness(7, 4, 7, 4),
                BorderThickness = new Thickness(0, 0, 1, 1),
            };
            cell.SetResourceReference(TableCell.BorderBrushProperty, "CardStrokeColorDefaultBrush");

            if (isHeader)
            {
                cell.SetResourceReference(TableCell.BackgroundProperty, "ControlFillColorSecondaryBrush");
            }

            var paragraph = new Paragraph
            {
                Margin = new Thickness(0),
                TextAlignment = c < alignments.Count ? alignments[c] : TextAlignment.Left,
            };

            AppendInlines(paragraph.Inlines, cells[c]);
            if (isHeader)
            {
                paragraph.FontWeight = FontWeights.SemiBold;
            }

            cell.Blocks.Add(paragraph);
            row.Cells.Add(cell);
        }

        return row;
    }

    private static List<string> SplitTableRow(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith('|'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.EndsWith('|'))
        {
            trimmed = trimmed[..^1];
        }

        var cells = new List<string>();
        foreach (var part in trimmed.Split('|'))
        {
            cells.Add(part.Trim());
        }

        return cells;
    }

    /// <summary>表格分隔行：只由 <c>| - : 空白</c> 组成，且至少有一个 <c>-</c>。</summary>
    private static bool IsTableSeparator(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || !trimmed.Contains('-'))
        {
            return false;
        }

        foreach (var ch in trimmed)
        {
            if (ch is not ('|' or '-' or ':' or ' ' or '\t'))
            {
                return false;
            }
        }

        return true;
    }

    private static List<TextAlignment> ParseAlignments(string separatorLine, int columnCount)
    {
        var alignments = new List<TextAlignment>();
        foreach (var cell in SplitTableRow(separatorLine))
        {
            var text = cell.Trim();
            var left = text.StartsWith(':');
            var right = text.EndsWith(':');
            alignments.Add(left && right
                ? TextAlignment.Center
                : right
                    ? TextAlignment.Right
                    : TextAlignment.Left);
        }

        while (alignments.Count < columnCount)
        {
            alignments.Add(TextAlignment.Left);
        }

        return alignments;
    }

    // ---------- 行内实现 ----------

    private static void AppendInlines(InlineCollection target, string? text)
    {
        var value = text ?? string.Empty;
        var position = 0;

        foreach (Match match in InlineRegex.Matches(value))
        {
            if (match.Index > position)
            {
                target.Add(new Run(DecodeEntities(value[position..match.Index])));
            }

            target.Add(BuildInline(match));
            position = match.Index + match.Length;
        }

        if (position < value.Length)
        {
            target.Add(new Run(DecodeEntities(value[position..])));
        }
    }

    private static Inline BuildInline(Match match)
    {
        if (match.Groups["code"].Success)
        {
            var code = match.Groups["code"].Value.Trim('`');
            var run = new Run(code) { FontFamily = MonoFont, FontSize = 13 };
            run.SetResourceReference(TextElement.BackgroundProperty, "ControlFillColorSecondaryBrush");
            return run;
        }

        if (match.Groups["image"].Success)
        {
            // 不下载图片（离线友好）：渲染成带 alt 文本的链接，点击去浏览器看。
            var url = match.Groups["imgurl"].Value;
            var alt = match.Groups["imgalt"].Value;
            var label = string.IsNullOrWhiteSpace(alt) ? url : alt.Trim();
            return CreateLink(url, label);
        }

        if (match.Groups["link"].Success)
        {
            var url = match.Groups["linkurl"].Value;
            var label = match.Groups["linktext"].Value;
            var link = CreateLink(url, string.Empty);
            AppendInlines(link.Inlines, string.IsNullOrEmpty(label) ? url : label);
            return link;
        }

        if (match.Groups["bold"].Success)
        {
            return CreateEmphasis(new Bold(), match.Groups["boldtext"].Value);
        }

        if (match.Groups["bold2"].Success)
        {
            return CreateEmphasis(new Bold(), match.Groups["boldtext2"].Value);
        }

        if (match.Groups["strike"].Success)
        {
            var span = new Span { TextDecorations = TextDecorations.Strikethrough };
            AppendInlines(span.Inlines, match.Groups["striketext"].Value);
            return span;
        }

        if (match.Groups["italic"].Success)
        {
            return CreateEmphasis(new Italic(), match.Groups["italictext"].Value);
        }

        if (match.Groups["italic2"].Success)
        {
            return CreateEmphasis(new Italic(), match.Groups["italictext2"].Value);
        }

        if (match.Groups["br"].Success)
        {
            return new LineBreak();
        }

        if (match.Groups["tag"].Success)
        {
            // 其余 HTML 标签直接剥掉，只保留其中的文字（库里的内容混有 <font>/<loc> 这类残留）。
            return new Run(string.Empty);
        }

        if (match.Groups["entity"].Success)
        {
            return new Run(DecodeEntities(match.Groups["entity"].Value));
        }

        return new Run(match.Value);
    }

    private static Inline CreateEmphasis(Span span, string text)
    {
        AppendInlines(span.Inlines, text);
        return span;
    }

    private static Hyperlink CreateLink(string url, string text)
    {
        var link = new Hyperlink { ToolTip = url, TextDecorations = null };
        link.SetResourceReference(TextElement.ForegroundProperty, "SystemAccentColorPrimaryBrush");

        if (!string.IsNullOrEmpty(text))
        {
            link.Inlines.Add(new Run(text));
        }

        // 说明书内链（如 characters/mail、market#steps）：没有 scheme、也不像网址，按手册 id 处理。
        // 转成 manual: 协议交给 OnRequestNavigate 在手册窗口里打开（不进浏览器）。
        if (ManualLinkRegex.IsMatch(url) && Uri.TryCreate("manual:" + url, UriKind.Absolute, out var manualUri))
        {
            link.NavigateUri = manualUri;
            link.RequestNavigate += OnRequestNavigate;
            return link;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            link.NavigateUri = uri;
        }
        else
        {
            // 不是绝对地址就只当普通文本展示（避免点了没反应还看不出来）。
            link.Inlines.Add(new Run(url));
            return link;
        }

        link.RequestNavigate += OnRequestNavigate;
        return link;
    }

    private static void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;

        // 手册内链：打开（或跳转）内置说明书窗口，而不是交给系统浏览器。
        if (string.Equals(e.Uri.Scheme, "manual", StringComparison.OrdinalIgnoreCase))
        {
            Views.Windows.ManualWindow.Show(e.Uri.AbsoluteUri["manual:".Length..]);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    // ---------- 小工具 ----------

    private static string[] SplitLines(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return [];
        }

        // 库里的正文混用 CRLF / CR / LF 三种换行（实测 ded.db 以 CR 为主），统一到 \n 再切。
        return markdown
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static int IndentWidth(string indent)
    {
        var width = 0;
        foreach (var ch in indent)
        {
            width += ch == '\t' ? 4 : 1;
        }

        return width;
    }

    private static string DecodeEntities(string text)
    {
        if (text.IndexOf('&') < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        var position = 0;

        while (position < text.Length)
        {
            var ampersand = text.IndexOf('&', position);
            if (ampersand < 0)
            {
                builder.Append(text, position, text.Length - position);
                break;
            }

            builder.Append(text, position, ampersand - position);

            var semicolon = text.IndexOf(';', ampersand + 1);
            if (semicolon < 0 || semicolon - ampersand > 10)
            {
                builder.Append('&');
                position = ampersand + 1;
                continue;
            }

            var entity = text[(ampersand + 1)..semicolon];
            var decoded = entity switch
            {
                "amp" => "&",
                "lt" => "<",
                "gt" => ">",
                "quot" => "\"",
                "apos" => "'",
                "nbsp" => " ",
                _ => null,
            };

            if (decoded is null && entity.Length > 1 && entity[0] == '#'
                && int.TryParse(entity[1..], out var code))
            {
                decoded = char.ConvertFromUtf32(code);
            }

            if (decoded is null)
            {
                builder.Append(text, ampersand, semicolon - ampersand + 1);
            }
            else
            {
                builder.Append(decoded);
            }

            position = semicolon + 1;
        }

        return builder.ToString();
    }
}
