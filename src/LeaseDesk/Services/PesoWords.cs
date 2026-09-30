namespace LeaseDesk.Services;

public static class PesoWords
{
    public static string Format(decimal amount)
    {
        var rounded = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (rounded < 0)
            rounded = 0;

        var pesos = (long)decimal.Truncate(rounded);
        var centavos = (int)decimal.Round((rounded - pesos) * 100, 0, MidpointRounding.AwayFromZero);
        if (centavos == 100)
        {
            pesos++;
            centavos = 0;
        }

        var words = ToWords(pesos) + (pesos == 1 ? " PESO" : " PESOS");
        if (centavos > 0)
            words += " AND " + ToWords(centavos) + (centavos == 1 ? " CENTAVO" : " CENTAVOS");

        return words;
    }

    public static string FormatMoney(decimal amount) =>
        amount.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-PH"));

    private static string ToWords(long number)
    {
        if (number == 0)
            return "ZERO";

        if (number < 0)
            return "MINUS " + ToWords(-number);

        var parts = new List<string>();
        AppendScale(parts, number / 1_000_000, "MILLION");
        AppendScale(parts, number / 1_000 % 1_000, "THOUSAND");
        var remainder = UnderOneThousand(number % 1_000);
        if (remainder.Length > 0)
            parts.Add(remainder);

        return string.Join(" ", parts);
    }

    private static void AppendScale(List<string> parts, long amount, string scale)
    {
        if (amount <= 0)
            return;

        parts.Add(UnderOneThousand(amount) + " " + scale);
    }

    private static string UnderOneThousand(long number)
    {
        var parts = new List<string>();
        if (number >= 100)
        {
            parts.Add(Ones(number / 100) + " HUNDRED");
            number %= 100;
        }

        if (number >= 20)
        {
            var tens = Tens(number / 10);
            var ones = number % 10;
            parts.Add(ones > 0 ? $"{tens}-{Ones(ones)}" : tens);
            number = 0;
        }
        else if (number >= 10)
        {
            parts.Add(Teens(number));
            number = 0;
        }

        if (number > 0)
            parts.Add(Ones(number));

        return string.Join(" ", parts);
    }

    private static string Ones(long number) => number switch
    {
        1 => "ONE",
        2 => "TWO",
        3 => "THREE",
        4 => "FOUR",
        5 => "FIVE",
        6 => "SIX",
        7 => "SEVEN",
        8 => "EIGHT",
        9 => "NINE",
        _ => ""
    };

    private static string Teens(long number) => number switch
    {
        10 => "TEN",
        11 => "ELEVEN",
        12 => "TWELVE",
        13 => "THIRTEEN",
        14 => "FOURTEEN",
        15 => "FIFTEEN",
        16 => "SIXTEEN",
        17 => "SEVENTEEN",
        18 => "EIGHTEEN",
        19 => "NINETEEN",
        _ => ""
    };

    private static string Tens(long number) => number switch
    {
        2 => "TWENTY",
        3 => "THIRTY",
        4 => "FORTY",
        5 => "FIFTY",
        6 => "SIXTY",
        7 => "SEVENTY",
        8 => "EIGHTY",
        9 => "NINETY",
        _ => ""
    };
}
