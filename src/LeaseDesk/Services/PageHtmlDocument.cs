using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Wordprocessing = DocumentFormat.OpenXml.Wordprocessing;

namespace LeaseDesk.Services;

public static partial class PageHtmlDocument
{
    public static byte[] Create(string html)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);
        var pages = document.QuerySelectorAll("section.docx").OfType<IElement>().ToList();
        if (pages.Count == 0 && document.Body is not null)
            pages.Add(document.Body);

        using var stream = new MemoryStream();
        using (var word = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            main.Document = new Wordprocessing.Document(new Body());
            var body = main.Document.Body!;
            AddStyles(main);
            AddNumbering(main);

            var wrote = false;
            foreach (var page in pages)
            {
                var blocks = Blocks(page).ToList();
                if (blocks.Count == 0)
                    continue;

                if (wrote)
                    body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));

                foreach (var block in blocks)
                    body.Append(block);
                wrote = true;
            }

            if (!wrote)
                return [];

            body.Append(Section(pages[0]));
            main.Document.Save();
        }

        return stream.ToArray();
    }

    private static IEnumerable<OpenXmlElement> Blocks(IElement container)
    {
        foreach (var child in container.Children)
        {
            switch (child.LocalName)
            {
                case "style":
                case "script":
                    break;
                case "table":
                    yield return Table(child);
                    break;
                case "ul":
                    foreach (var item in List(child, bullet: true))
                        yield return item;
                    break;
                case "ol":
                    foreach (var item in List(child, bullet: false))
                        yield return item;
                    break;
                case "p":
                case "h1":
                case "h2":
                case "h3":
                case "li":
                    yield return Paragraph(child, numberingId: null);
                    break;
                default:
                    if (child.QuerySelector("p, table, ul, ol, h1, h2, h3") is not null)
                    {
                        foreach (var nested in Blocks(child))
                            yield return nested;
                    }
                    else if (HasVisibleText(child))
                    {
                        yield return Paragraph(child, numberingId: null);
                    }
                    break;
            }
        }
    }

    private static IEnumerable<Paragraph> List(IElement list, bool bullet)
    {
        var numberingId = bullet ? 1 : 2;
        foreach (var item in list.Children.Where(child => child.LocalName == "li"))
        {
            var paragraphs = item.Children.Where(child => child.LocalName is "p" or "h1" or "h2" or "h3" or "div").ToList();
            if (paragraphs.Count == 0)
            {
                yield return Paragraph(item, numberingId);
                continue;
            }

            foreach (var paragraph in paragraphs)
                yield return Paragraph(paragraph, numberingId);
        }
    }

    private static Paragraph Paragraph(IElement source, int? numberingId)
    {
        var heading = source.LocalName switch
        {
            "h1" => "Heading1",
            "h2" => "Heading2",
            "h3" => "Heading3",
            _ => null
        };

        var paragraph = new Paragraph();
        var properties = new ParagraphProperties();
        if (heading is not null)
            properties.Append(new ParagraphStyleId { Val = heading });

        var align = Css(source, "text-align");
        var justification = align?.ToLowerInvariant() switch
        {
            "center" => JustificationValues.Center,
            "right" or "end" => JustificationValues.Right,
            "justify" or "both" => JustificationValues.Both,
            _ => (JustificationValues?)null
        };
        if (justification is not null)
            properties.Append(new Justification { Val = justification });

        var line = LineSpacing(Css(source, "line-height"));
        if (line is not null)
            properties.Append(new SpacingBetweenLines { Line = line, LineRule = LineSpacingRuleValues.Auto });

        if (numberingId is not null)
        {
            properties.Append(new NumberingProperties(
                new NumberingLevelReference { Val = 0 },
                new NumberingId { Val = numberingId }));
        }

        if (properties.HasChildren)
            paragraph.Append(properties);

        foreach (var run in Runs(source, heading))
            paragraph.Append(run);

        if (!paragraph.Elements<Run>().Any())
            paragraph.Append(new Run(new Text(string.Empty)));

        return paragraph;
    }

    private static IEnumerable<Run> Runs(IElement source, string? heading)
    {
        var runs = new List<Run>();
        var fallback = heading switch
        {
            "Heading1" => new RunStyle(true, false, false, null, 32, null),
            "Heading2" => new RunStyle(true, false, false, null, 28, null),
            "Heading3" => new RunStyle(true, false, false, null, 24, null),
            _ => RunStyle.Empty
        };
        Walk(source, fallback, runs, root: true);
        return runs;
    }

    private static void Walk(INode node, RunStyle style, List<Run> runs, bool root)
    {
        if (node is IText text)
        {
            if (text.Text.Length > 0)
                runs.Add(Run(text.Text, style));
            return;
        }

        if (node is not IElement element)
            return;

        if (!root && element.LocalName is "table" or "ul" or "ol" or "p" or "h1" or "h2" or "h3" or "section" or "article")
            return;

        if (element.LocalName == "br")
        {
            runs.Add(new Run(new Break()));
            return;
        }

        var next = style.Merge(element);
        foreach (var child in element.ChildNodes)
            Walk(child, next, runs, root: false);
    }

    private static Run Run(string text, RunStyle style)
    {
        var run = new Run();
        var properties = new RunProperties();
        if (style.Bold)
            properties.Append(new Bold());
        if (style.Italic)
            properties.Append(new Italic());
        if (style.Underline)
            properties.Append(new Underline { Val = UnderlineValues.Single });
        if (!string.IsNullOrEmpty(style.Font))
        {
            properties.Append(new RunFonts
            {
                Ascii = style.Font,
                HighAnsi = style.Font,
                ComplexScript = style.Font
            });
        }

        if (style.HalfPoints is > 0)
        {
            var size = style.HalfPoints.Value.ToString(CultureInfo.InvariantCulture);
            properties.Append(new FontSize { Val = size });
            properties.Append(new FontSizeComplexScript { Val = size });
        }

        if (!string.IsNullOrEmpty(style.Color))
            properties.Append(new Color { Val = style.Color });

        if (properties.HasChildren)
            run.Append(properties);

        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private static Table Table(IElement source)
    {
        var table = new Table();
        var properties = new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Nil },
                new LeftBorder { Val = BorderValues.Nil },
                new BottomBorder { Val = BorderValues.Nil },
                new RightBorder { Val = BorderValues.Nil },
                new InsideHorizontalBorder { Val = BorderValues.Nil },
                new InsideVerticalBorder { Val = BorderValues.Nil }));
        table.Append(properties);

        foreach (var rowElement in source.QuerySelectorAll("tr"))
        {
            if (rowElement.ParentElement?.Closest("table") != source)
                continue;

            var row = new TableRow();
            foreach (var cellElement in rowElement.Children.Where(child => child.LocalName is "td" or "th"))
            {
                var cell = new TableCell();
                var cellProperties = new TableCellProperties();
                var width = Css(cellElement, "width");
                if (!string.IsNullOrEmpty(width))
                {
                    cellProperties.Append(new TableCellWidth
                    {
                        Type = TableWidthUnitValues.Dxa,
                        Width = Math.Max(Twips(width, 1440), 200).ToString(CultureInfo.InvariantCulture)
                    });
                }

                cellProperties.Append(CellBorders(cellElement.GetAttribute("style") ?? ""));
                if (cellElement.LocalName == "th")
                    cellProperties.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = "auto" });
                cell.Append(cellProperties);

                var blocks = Blocks(cellElement).ToList();
                if (blocks.Count == 0)
                    cell.Append(new Paragraph());
                else
                    foreach (var block in blocks)
                        cell.Append(block);

                row.Append(cell);
            }

            if (row.HasChildren)
                table.Append(row);
        }

        return table;
    }

    private static TableCellBorders CellBorders(string style) => new(
        (TopBorder)Border(style, "top"),
        (LeftBorder)Border(style, "left"),
        (BottomBorder)Border(style, "bottom"),
        (RightBorder)Border(style, "right"));

    private static OpenXmlElement Border(string style, string side)
    {
        var visible = BorderVisible(style, side);
        return side switch
        {
            "top" => new TopBorder { Val = visible ? BorderValues.Single : BorderValues.Nil, Size = 4, Color = "000000" },
            "left" => new LeftBorder { Val = visible ? BorderValues.Single : BorderValues.Nil, Size = 4, Color = "000000" },
            "bottom" => new BottomBorder { Val = visible ? BorderValues.Single : BorderValues.Nil, Size = 4, Color = "000000" },
            _ => new RightBorder { Val = visible ? BorderValues.Single : BorderValues.Nil, Size = 4, Color = "000000" }
        };
    }

    private static bool BorderVisible(string style, string side)
    {
        var all = BorderPattern().Match(style);
        if (all.Success && VisibleBorder(all.Groups[1].Value))
            return true;

        var specific = Regex.Match(style, $@"(?:^|;)\s*border-{side}\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
        return specific.Success && VisibleBorder(specific.Groups[1].Value);
    }

    private static bool VisibleBorder(string value) =>
        !value.Contains("none", StringComparison.OrdinalIgnoreCase)
        && !value.Contains("hidden", StringComparison.OrdinalIgnoreCase)
        && DigitPattern().IsMatch(value);

    private static SectionProperties Section(IElement page)
    {
        var width = Twips(Css(page, "width"), 12240);
        var height = Twips(Css(page, "min-height") ?? Css(page, "height"), 15840);
        var padding = Padding(Css(page, "padding"));
        var top = Css(page, "padding-top");
        var right = Css(page, "padding-right");
        var bottom = Css(page, "padding-bottom");
        var left = Css(page, "padding-left");
        if (top is not null) padding.Top = Twips(top, padding.Top);
        if (right is not null) padding.Right = Twips(right, padding.Right);
        if (bottom is not null) padding.Bottom = Twips(bottom, padding.Bottom);
        if (left is not null) padding.Left = Twips(left, padding.Left);

        if (width < 4000) width = 12240;
        if (height < 4000) height = 15840;

        return new SectionProperties(
            new PageSize { Width = (uint)width, Height = (uint)height },
            new PageMargin
            {
                Top = padding.Top,
                Bottom = padding.Bottom,
                Left = (uint)Math.Max(padding.Left, 0),
                Right = (uint)Math.Max(padding.Right, 0),
                Header = 720,
                Footer = 720,
                Gutter = 0
            });
    }

    private static void AddStyles(MainDocumentPart main)
    {
        var styles = main.AddNewPart<StyleDefinitionsPart>();
        styles.Styles = new Styles(
            new DocDefaults(new RunPropertiesDefault(new RunPropertiesBaseStyle(
                new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" },
                new FontSize { Val = "24" }))),
            new Style(
                new StyleName { Val = "Normal" },
                new PrimaryStyle(),
                new StyleRunProperties(
                    new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" },
                    new FontSize { Val = "24" }))
            {
                Type = StyleValues.Paragraph,
                StyleId = "Normal",
                Default = true
            },
            Heading("Heading1", "heading 1", "32"),
            Heading("Heading2", "heading 2", "28"),
            Heading("Heading3", "heading 3", "24"));
        styles.Styles.Save();
    }

    private static Style Heading(string id, string name, string halfPoints) => new(
        new StyleName { Val = name },
        new BasedOn { Val = "Normal" },
        new PrimaryStyle(),
        new StyleRunProperties(
            new Bold(),
            new FontSize { Val = halfPoints }))
    {
        Type = StyleValues.Paragraph,
        StyleId = id
    };

    private static void AddNumbering(MainDocumentPart main)
    {
        var part = main.AddNewPart<NumberingDefinitionsPart>();
        var numbering = new Numbering();
        numbering.Append(Abstract(1, NumberFormatValues.Bullet, "•"));
        numbering.Append(Abstract(2, NumberFormatValues.Decimal, "%1."));
        numbering.Append(new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = 1 });
        numbering.Append(new NumberingInstance(new AbstractNumId { Val = 2 }) { NumberID = 2 });
        part.Numbering = numbering;
    }

    private static AbstractNum Abstract(int id, NumberFormatValues format, string text)
    {
        var level = new Level { LevelIndex = 0 };
        level.Append(new StartNumberingValue { Val = 1 });
        level.Append(new NumberingFormat { Val = format });
        level.Append(new LevelText { Val = text });
        level.Append(new LevelJustification { Val = LevelJustificationValues.Left });
        level.Append(new ParagraphProperties(new Indentation { Left = "720", Hanging = "360" }));

        var abs = new AbstractNum(new MultiLevelType { Val = MultiLevelValues.HybridMultilevel })
        {
            AbstractNumberId = id
        };
        abs.Append(level);
        return abs;
    }

    private static RunStyle MergeStyle(RunStyle current, IElement element)
    {
        var bold = current.Bold || element.LocalName is "b" or "strong";
        var italic = current.Italic || element.LocalName is "i" or "em";
        var underline = current.Underline || element.LocalName == "u";
        var font = current.Font;
        var size = current.HalfPoints;
        var color = current.Color;

        var weight = Css(element, "font-weight");
        if (weight is "bold" or "bolder" || (int.TryParse(weight, out var weightNumber) && weightNumber >= 600))
            bold = true;
        if (Css(element, "font-style") is "italic" or "oblique")
            italic = true;
        var decoration = Css(element, "text-decoration") ?? Css(element, "text-decoration-line");
        if (decoration?.Contains("underline", StringComparison.OrdinalIgnoreCase) == true)
            underline = true;

        var family = Css(element, "font-family") ?? element.GetAttribute("face");
        var parsedFont = FirstFont(family);
        if (parsedFont is not null)
            font = parsedFont;

        var parsedSize = HalfPoints(Css(element, "font-size"));
        if (parsedSize is null && element.GetAttribute("size") is { } htmlSize && int.TryParse(htmlSize, out var legacy))
            parsedSize = legacy switch { 1 => 16, 2 => 20, 3 => 24, 4 => 28, 5 => 36, 6 => 48, _ => 72 };
        if (parsedSize is not null)
            size = parsedSize;

        var parsedColor = Hex(Css(element, "color"));
        if (parsedColor is not null)
            color = parsedColor;

        return new RunStyle(bold, italic, underline, font, size, color);
    }

    private static string? FirstFont(string? family)
    {
        if (string.IsNullOrWhiteSpace(family))
            return null;

        var first = family.Split(',')[0].Trim().Trim('"', '\'');
        if (first.Length == 0 || first.Equals("inherit", StringComparison.OrdinalIgnoreCase))
            return null;
        return first;
    }

    private static int? HalfPoints(string? size)
    {
        if (string.IsNullOrWhiteSpace(size))
            return null;

        var value = size.Trim().ToLowerInvariant();
        if (value is "inherit" or "initial" or "unset")
            return null;

        if (value.EndsWith("pt", StringComparison.Ordinal) && double.TryParse(value[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var points))
            return (int)Math.Round(points * 2);
        if (value.EndsWith("px", StringComparison.Ordinal) && double.TryParse(value[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pixels))
            return (int)Math.Round(pixels * 1.5);
        return null;
    }

    private static string? Hex(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return null;

        var value = color.Trim();
        if (value.StartsWith('#'))
        {
            var hex = value[1..];
            if (hex.Length == 3)
                hex = string.Concat(hex.Select(item => $"{item}{item}"));
            return hex.Length == 6 ? hex.ToUpperInvariant() : null;
        }

        var rgb = RgbPattern().Match(value);
        if (!rgb.Success)
            return null;

        return string.Create(CultureInfo.InvariantCulture, $"{Byte(rgb, 1):X2}{Byte(rgb, 2):X2}{Byte(rgb, 3):X2}");
    }

    private static int Byte(Match match, int group)
    {
        var number = double.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
        return (int)Math.Clamp(Math.Round(number), 0, 255);
    }

    private static string? LineSpacing(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var unitless))
            return ((int)Math.Round(unitless * 240)).ToString(CultureInfo.InvariantCulture);

        return null;
    }

    private static string? Css(IElement element, string name)
    {
        var style = element.GetAttribute("style");
        if (string.IsNullOrEmpty(style))
            return null;

        foreach (var part in style.Split(';'))
        {
            var bits = part.Split(':', 2);
            if (bits.Length == 2 && bits[0].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                return bits[1].Trim();
        }

        return null;
    }

    private static (int Top, int Right, int Bottom, int Left) Padding(string? padding)
    {
        if (string.IsNullOrWhiteSpace(padding))
            return (1440, 1440, 1440, 1440);

        var parts = padding.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int Side(string item) => Twips(item, 1440);
        return parts.Length switch
        {
            1 => (Side(parts[0]), Side(parts[0]), Side(parts[0]), Side(parts[0])),
            2 => (Side(parts[0]), Side(parts[1]), Side(parts[0]), Side(parts[1])),
            3 => (Side(parts[0]), Side(parts[1]), Side(parts[2]), Side(parts[1])),
            _ => (Side(parts[0]), Side(parts[1]), Side(parts[2]), Side(parts[3]))
        };
    }

    private static int Twips(string? value, int fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var text = value.Trim().ToLowerInvariant();
        if (text.EndsWith("pt", StringComparison.Ordinal) && double.TryParse(text[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var points))
            return (int)Math.Round(points * 20);
        if (text.EndsWith("px", StringComparison.Ordinal) && double.TryParse(text[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pixels))
            return (int)Math.Round(pixels * 15);
        if (text.EndsWith("in", StringComparison.Ordinal) && double.TryParse(text[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var inches))
            return (int)Math.Round(inches * 1440);
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var plain))
            return (int)Math.Round(plain * 15);
        return fallback;
    }

    private static bool HasVisibleText(IElement element) =>
        !string.IsNullOrWhiteSpace(element.TextContent);

    private readonly record struct RunStyle(bool Bold, bool Italic, bool Underline, string? Font, int? HalfPoints, string? Color)
    {
        public static RunStyle Empty => new(false, false, false, null, null, null);

        public RunStyle Merge(IElement element) => MergeStyle(this, element);
    }

    [GeneratedRegex(@"(?:^|;)\s*border\s*:\s*([^;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex BorderPattern();

    [GeneratedRegex(@"[1-9]")]
    private static partial Regex DigitPattern();

    [GeneratedRegex(@"rgba?\(\s*(\d+(?:\.\d+)?)\s*,\s*(\d+(?:\.\d+)?)\s*,\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex RgbPattern();
}
