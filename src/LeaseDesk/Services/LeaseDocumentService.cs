using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LeaseDesk.Data;

namespace LeaseDesk.Services;

public class LeaseDocumentService
{
    private readonly string _templatePath;

    public LeaseDocumentService(IWebHostEnvironment environment)
    {
        _templatePath = Path.Combine(environment.ContentRootPath, "Templates", "horizon-lease.docx");
    }

    public byte[] CreateWord(LeaseContract contract)
    {
        if (!File.Exists(_templatePath))
            throw new InvalidOperationException("The Horizon lease template is missing.");

        var bytes = File.ReadAllBytes(_templatePath);
        using var stream = new MemoryStream();
        stream.Write(bytes);
        stream.Position = 0;

        using (var document = WordprocessingDocument.Open(stream, true))
        {
            var replacements = BuildReplacements(contract);
            var parts = new List<OpenXmlPart>();
            if (document.MainDocumentPart is not null)
            {
                parts.Add(document.MainDocumentPart);
                parts.AddRange(document.MainDocumentPart.HeaderParts);
                parts.AddRange(document.MainDocumentPart.FooterParts);
            }

            foreach (var part in parts)
            {
                if (part.RootElement is null)
                    continue;
                foreach (var paragraph in part.RootElement.Descendants<Paragraph>())
                    ReplaceInParagraph(paragraph, replacements);
            }

            document.MainDocumentPart?.Document?.Save();
        }

        return stream.ToArray();
    }

    public IReadOnlyList<string> ReadParagraphs(byte[] docx)
    {
        using var stream = new MemoryStream(docx);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        return body?.Descendants<Paragraph>()
            .Select(paragraph => string.Concat(paragraph.Descendants<Text>().Select(text => text.Text)).Trim())
            .Where(text => text.Length > 0)
            .ToList()
            ?? [];
    }

    public static IReadOnlyList<string> MissingFields(LeaseContract contract)
    {
        var missing = new List<string>();
        void Need(string? value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
                missing.Add(label);
        }

        Need(contract.LessorName, "Lessor name");
        Need(contract.LessorAddress, "Lessor address");
        Need(contract.LesseeName, "Lessee name");
        Need(contract.LesseeAddress, "Lessee address");
        Need(contract.PropertyName, "Property");
        Need(contract.Unit, "Unit");
        Need(contract.PropertyAddress, "Property address");
        Need(contract.UnitType, "Unit type");
        Need(contract.SigningPlace, "Signing place");
        Need(contract.WitnessOneName, "First witness");
        Need(contract.WitnessTwoName, "Second witness");
        if (contract.MonthlyRent <= 0)
            missing.Add("Monthly rent");
        if (contract.TermYears < 1)
            missing.Add("Term");
        if (contract.StartDate == default)
            missing.Add("Start date");
        if (contract.SigningDate == default)
            missing.Add("Signing date");
        if (contract.ReceiptDate == default)
            missing.Add("Date received");
        return missing;
    }

    public static string DownloadName(LeaseContract contract, string extension)
    {
        var raw = $"{contract.PropertyName}-{contract.Unit}-{contract.LesseeName}";
        var builder = new StringBuilder();
        foreach (var character in raw)
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(character);
            else if (builder.Length == 0 || builder[^1] != '-')
                builder.Append('-');
        }

        var stem = builder.ToString().Trim('-');
        if (stem.Length == 0)
            stem = "lease";
        return $"{stem}.{extension}";
    }

    private static IReadOnlyList<(string Old, string New)> BuildReplacements(LeaseContract contract)
    {
        var rentWords = PesoWords.Format(contract.MonthlyRent);
        var rentNumber = PesoNumber(contract.MonthlyRent);
        var advanceWords = PesoWords.Format(contract.AdvanceAmount);
        var advanceNumber = PesoNumber(contract.AdvanceAmount);
        var depositWords = PesoWords.Format(contract.SecurityDepositAmount);
        var depositNumber = PesoNumber(contract.SecurityDepositAmount);
        var receiptNumber = contract.ReceiptAmount.ToString("N2", CultureInfo.GetCultureInfo("en-PH"));
        var payer = string.IsNullOrWhiteSpace(contract.ReceiptPayer) ? contract.LesseeName : contract.ReceiptPayer;
        var representative = contract.LessorRepresentative.Trim();
        var lessorIntro = string.IsNullOrEmpty(representative)
            ? $"{contract.LessorName}, of legal age, Filipino, and a resident at {contract.LessorAddress}."
            : $"{contract.LessorName} with representative {representative}, of legal age, Filipino, and a resident at {contract.LessorAddress}.";
        var property = $"{contract.PropertyName}, {contract.Unit}, {contract.PropertyAddress}";
        var term = $"This lease commences on {LongDate(contract.StartDate)} and shall be in force for {CountPhrase(contract.TermYears)} years locked in until {LongDate(contract.EndDate)}";
        var occupants = $"{contract.UnitType.ToLowerInvariant()} unit with a maximum of {CountPhrase(contract.MaxOccupants)} occupants";
        var advancePhrase = CountPhrase(contract.AdvanceMonths) + (contract.AdvanceMonths == 1 ? " month" : " months");
        var depositPhrase = $"({contract.SecurityDepositMonths}) months";

        return
        [
            ("ALMA ORAPA with representative ANGELITA ORAPA, of legal age, Filipino, and a resident at Poblacion Pilar Bohol, Philippines.", lessorIntro),
            ("GILBERTO ANTON MAGDUA MONTILLA, of legal age, Filipino, and resident at Purok Manga Guiwanon Baclayon Bohol, Philippines.",
                $"{contract.LesseeName}, of legal age, Filipino, and resident at {contract.LesseeAddress}."),
            ("Horizon 101, Tower 2, unit 34A, Mango Avenue Cebu City", property),
            ("This lease commences on January 20,2026 and shall be in force for three (3) years locked in until January 20, 2029", term),
            ("one (1) month advance rental of FIFTEEN THOUSAND PESOS (P15,000)",
                $"{advancePhrase} advance rental of {advanceWords} (P{advanceNumber})"),
            ("(2) months security deposit of THIRTY THOUSAND PESOS (P30,000)",
                $"{depositPhrase} security deposit of {depositWords} (P{depositNumber})"),
            ("FIFTEEN THOUSAND PESOS (PHP 15,000)", $"{rentWords} (PHP {rentNumber})"),
            ("35 POST DATED CHECKS", $"{contract.PostDatedCheckCount} POST DATED CHECKS"),
            ("studio unit with a maximum of three (3) occupants", occupants),
            ("DONE this January 20th, 2026, in Cebu City, Cebu, Philippines.",
                $"DONE this {OrdinalDate(contract.SigningDate)}, in {contract.SigningPlace}."),
            ("ALMA ORAPA received thePAYMENT amounting (45,000.00) on January 6 2026, from GILBERTO ANTON MAGBUA MONTILLA",
                $"{contract.LessorName} received the PAYMENT amounting ({receiptNumber}) on {ReceiptDate(contract.ReceiptDate)}, from {payer}"),
            ("GILBERTO ANTON M. MONTILLA", contract.LesseeName),
            ("GILBERTO ANTON MAGDUA MONTILLA", contract.LesseeName),
            ("GILBERTO ANTON MAGBUA MONTILLA", payer),
            ("MARIA TERESA AQUINO", contract.WitnessOneName),
            ("ROSENDA AKUT", contract.WitnessTwoName),
            ("ANGELITA ORAPA", representative),
            ("ALMA ORAPA", contract.LessorName)
        ];
    }

    private static void ReplaceInParagraph(Paragraph paragraph, IReadOnlyList<(string Old, string New)> replacements)
    {
        foreach (var (oldValue, newValue) in replacements)
        {
            if (oldValue.Length == 0 || oldValue == newValue)
                continue;

            var texts = paragraph.Descendants<Text>().ToList();
            if (texts.Count == 0)
                return;

            var combined = string.Concat(texts.Select(text => text.Text));
            var indexes = new List<int>();
            var searchFrom = 0;
            while (searchFrom <= combined.Length - oldValue.Length)
            {
                var index = combined.IndexOf(oldValue, searchFrom, StringComparison.Ordinal);
                if (index < 0)
                    break;
                indexes.Add(index);
                searchFrom = index + oldValue.Length;
            }

            for (var i = indexes.Count - 1; i >= 0; i--)
            {
                texts = paragraph.Descendants<Text>().ToList();
                ReplaceSpan(texts, indexes[i], oldValue.Length, newValue);
            }
        }
    }

    private static void ReplaceSpan(List<Text> texts, int start, int length, string replacement)
    {
        var end = start + length;
        var cursor = 0;
        Text? first = null;
        var prefix = "";
        var suffix = "";

        foreach (var text in texts)
        {
            var nodeStart = cursor;
            var nodeEnd = cursor + text.Text.Length;
            cursor = nodeEnd;
            if (nodeEnd <= start || nodeStart >= end)
                continue;

            var localStart = Math.Max(0, start - nodeStart);
            var localEnd = Math.Min(text.Text.Length, end - nodeStart);
            if (first is null)
            {
                first = text;
                prefix = text.Text[..localStart];
            }

            if (nodeEnd >= end)
                suffix = text.Text[localEnd..];

            text.Text = "";
        }

        if (first is null)
            return;

        first.Text = prefix + replacement + suffix;
        first.Space = SpaceProcessingModeValues.Preserve;
    }

    private static string PesoNumber(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero).ToString("#,##0.##", CultureInfo.GetCultureInfo("en-PH"));

    private static string LongDate(DateOnly date) => date.ToString("MMMM d, yyyy", CultureInfo.GetCultureInfo("en-US"));

    private static string ReceiptDate(DateOnly date) => date.ToString("MMMM d yyyy", CultureInfo.GetCultureInfo("en-US"));

    private static string OrdinalDate(DateOnly date)
    {
        var suffix = date.Day % 100 is 11 or 12 or 13
            ? "th"
            : (date.Day % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };
        return $"{date.ToString("MMMM", CultureInfo.GetCultureInfo("en-US"))} {date.Day}{suffix}, {date.Year}";
    }

    private static string CountPhrase(int count) => $"{CountWord(count)} ({count})";

    private static string CountWord(int count) => count switch
    {
        0 => "zero",
        1 => "one",
        2 => "two",
        3 => "three",
        4 => "four",
        5 => "five",
        6 => "six",
        7 => "seven",
        8 => "eight",
        9 => "nine",
        10 => "ten",
        11 => "eleven",
        12 => "twelve",
        _ => count.ToString(CultureInfo.InvariantCulture)
    };
}
