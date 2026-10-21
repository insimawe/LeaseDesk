using System.Text.RegularExpressions;

namespace LeaseDesk.Services;

public static partial class PagePrintHtml
{
    public static string Wrap(string html)
    {
        var width = Measure(html, "width") ?? "595.3pt";
        var height = Measure(html, "min-height") ?? Measure(html, "height") ?? "841.9pt";
        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>@page { size: "
            + width + " " + height + "; margin: 0; } html, body { margin: 0; padding: 0; background: #fff; }</style></head><body>"
            + html
            + "<style>"
            + "html, body, .docx-wrapper { margin: 0 !important; padding: 0 !important; background: #fff !important; width: " + width + " !important; }"
            + "section.docx { box-sizing: border-box !important; zoom: 1 !important; width: " + width + " !important; height: " + height + " !important; min-height: " + height + " !important; max-height: " + height + " !important; margin: 0 !important; box-shadow: none !important; overflow: hidden !important; break-after: page; page-break-after: always; break-inside: auto; page-break-inside: auto; }"
            + "section.docx:last-of-type { break-after: auto; page-break-after: auto; }"
            + "section.docx * { break-before: auto !important; break-after: auto !important; page-break-before: auto !important; page-break-after: auto !important; }"
            + "</style></body></html>";
    }

    private static string? Measure(string html, string property)
    {
        foreach (Match section in SectionPattern().Matches(html))
        {
            if (!section.Groups[1].Value.Contains("docx", StringComparison.OrdinalIgnoreCase))
                continue;

            var style = StylePattern().Match(section.Groups[1].Value);
            var inline = style.Success ? Length(style.Groups[1].Value, property) : null;
            if (inline is not null)
                return inline;
        }

        var rule = Regex.Match(
            html,
            @"docx[^{]*\{[^}]*" + property + @"\s*:\s*([^;}]+)",
            RegexOptions.IgnoreCase);
        return rule.Success ? Normalize(rule.Groups[1].Value) : null;
    }

    private static string? Length(string style, string property)
    {
        var part = Regex.Match(style, $@"(?:^|;)\s*{property}\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
        return part.Success ? Normalize(part.Groups[1].Value) : null;
    }

    private static string? Normalize(string value)
    {
        value = value.Trim().Trim('"', '\'');
        return value.EndsWith("pt", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith("px", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith("in", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith("mm", StringComparison.OrdinalIgnoreCase)
            ? value
            : null;
    }

    [GeneratedRegex("""<section\b([^>]*)>""", RegexOptions.IgnoreCase)]
    private static partial Regex SectionPattern();

    [GeneratedRegex("""style="([^"]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex StylePattern();
}
