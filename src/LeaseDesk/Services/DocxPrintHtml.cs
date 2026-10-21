using System.Globalization;
using System.Net;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace LeaseDesk.Services;

public static class DocxPrintHtml
{
    public static string Create(byte[] docx)
    {
        using var stream = new MemoryStream(docx);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>");
        html.Append(PageStyle(body));
        html.Append("body{font-family:'Times New Roman',Times,serif;font-size:12pt;color:#1a2744;}");
        html.Append("p{margin:0 0 8pt;} table{width:100%;border-collapse:collapse;margin:0 0 8pt;} td{vertical-align:top;padding:2pt 6pt;}");
        html.Append("</style></head><body>");

        if (body is not null)
        {
            foreach (var child in body.ChildElements)
                AppendBlock(html, child);
        }

        html.Append("</body></html>");
        return html.ToString();
    }

    private static string PageStyle(Body? body)
    {
        var section = body?.Elements<SectionProperties>().LastOrDefault();
        var size = section?.GetFirstChild<PageSize>();
        var margin = section?.GetFirstChild<PageMargin>();
        var width = Inches(size?.Width?.Value, 8.27);
        var height = Inches(size?.Height?.Value, 11.69);
        var top = Inches(margin?.Top?.Value, 1);
        var right = Inches(margin?.Right?.Value, 1);
        var bottom = Inches(margin?.Bottom?.Value, 1);
        var left = Inches(margin?.Left?.Value, 1);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"@page{{size:{width:0.##}in {height:0.##}in;margin:{top:0.##}in {right:0.##}in {bottom:0.##}in {left:0.##}in;}}");
    }

    private static double Inches(uint? twips, double fallback) =>
        twips is > 0 ? twips.Value / 1440d : fallback;

    private static double Inches(int? twips, double fallback) =>
        twips is > 0 ? twips.Value / 1440d : fallback;

    private static void AppendBlock(StringBuilder html, OpenXmlElement element)
    {
        switch (element)
        {
            case Paragraph paragraph:
                AppendParagraph(html, paragraph);
                break;
            case Table table:
                AppendTable(html, table);
                break;
        }
    }

    private static void AppendParagraph(StringBuilder html, Paragraph paragraph)
    {
        if (paragraph.Descendants<Break>().Any(item => item.Type?.InnerText == "page"))
            html.Append("<div style=\"break-before:page\"></div>");

        var align = paragraph.ParagraphProperties?.Justification?.Val?.InnerText switch
        {
            "center" => "center",
            "right" => "right",
            "both" => "justify",
            _ => "left"
        };
        html.Append(CultureInfo.InvariantCulture, $"<p style=\"text-align:{align}\">");
        foreach (var run in paragraph.Elements<Run>())
            AppendRun(html, run);
        if (!paragraph.Elements<Run>().Any())
            html.Append("<br>");
        html.Append("</p>");
    }

    private static void AppendRun(StringBuilder html, Run run)
    {
        var text = new StringBuilder();
        foreach (var child in run.ChildElements)
        {
            switch (child)
            {
                case Text item:
                    text.Append(item.Text);
                    break;
                case Break item when item.Type?.InnerText != "page":
                    text.Append('\n');
                    break;
            }
        }

        if (text.Length == 0)
            return;

        var encoded = WebUtility.HtmlEncode(text.ToString()).Replace("\n", "<br>", StringComparison.Ordinal);
        var properties = run.RunProperties;
        var style = new StringBuilder();
        if (properties?.Bold is not null)
            style.Append("font-weight:700;");
        if (properties?.Italic is not null)
            style.Append("font-style:italic;");
        if (properties?.Underline is not null)
            style.Append("text-decoration:underline;");
        var size = properties?.FontSize?.Val?.Value;
        if (int.TryParse(size, out var halfPoints) && halfPoints > 0)
            style.Append(CultureInfo.InvariantCulture, $"font-size:{halfPoints / 2d:0.##}pt;");

        if (style.Length == 0)
            html.Append(encoded);
        else
            html.Append(CultureInfo.InvariantCulture, $"<span style=\"{style}\">{encoded}</span>");
    }

    private static void AppendTable(StringBuilder html, Table table)
    {
        html.Append("<table>");
        foreach (var row in table.Elements<TableRow>())
        {
            html.Append("<tr>");
            foreach (var cell in row.Elements<TableCell>())
            {
                html.Append("<td style=\"");
                html.Append(CellStyle(cell));
                html.Append("\">");
                foreach (var child in cell.ChildElements)
                    AppendBlock(html, child);
                html.Append("</td>");
            }

            html.Append("</tr>");
        }

        html.Append("</table>");
    }

    private static string CellStyle(TableCell cell)
    {
        var borders = cell.TableCellProperties?.TableCellBorders;
        var style = new StringBuilder();
        Border(style, "top", borders?.TopBorder);
        Border(style, "right", borders?.RightBorder);
        Border(style, "bottom", borders?.BottomBorder);
        Border(style, "left", borders?.LeftBorder);
        return style.ToString();
    }

    private static void Border(StringBuilder style, string side, BorderType? border)
    {
        var value = border?.Val?.InnerText;
        if (string.IsNullOrEmpty(value) || value is "nil" or "none")
            return;

        style.Append(CultureInfo.InvariantCulture, $"border-{side}:1px solid #1a2744;");
    }
}
