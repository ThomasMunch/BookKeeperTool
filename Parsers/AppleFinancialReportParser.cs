using System.Globalization;
using BookKeeperTool.Models;

namespace BookKeeperTool.Parsers;

public static class AppleFinancialReportParser
{
    // Maps month names (English) to month numbers
    private static readonly Dictionary<string, int> MonthNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["January"] = 1, ["February"] = 2, ["March"] = 3, ["April"] = 4,
        ["May"] = 5, ["June"] = 6, ["July"] = 7, ["August"] = 8,
        ["September"] = 9, ["October"] = 10, ["November"] = 11, ["December"] = 12
    };

    // Maps country/region labels in the file to ISO currency codes
    private static readonly Dictionary<string, string> RegionToCurrency = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Brazil (BRL)"]         = "BRL",
        ["Canada (CAD)"]         = "CAD",
        ["Denmark (DKK)"]        = "DKK",
        ["Euro-Zone (EUR)"]      = "EUR",
        ["United Kingdom (GBP)"] = "GBP",
        ["Norway (NOK)"]         = "NOK",
        ["Sweden (SEK)"]         = "SEK",
        ["Americas (USD)"]       = "USD",
        ["Japan (JPY)"]          = "JPY",
        ["Australia (AUD)"]      = "AUD",
        ["Switzerland (CHF)"]    = "CHF",
        ["Mexico (MXN)"]         = "MXN",
        ["India (INR)"]          = "INR",
    };

    /// <summary>
    /// Parses a financial_report*.csv and returns the report date, currency rates, and whether payment has been made.
    /// The date is extracted from the title line, e.g. "iTunes Connect - Payments and Financial Reports (March, 2026)".
    /// The Exchange Rate column gives the DKK rate for each currency.
    /// IsPaid is true when the file contains a "Paid to ..." line.
    /// </summary>
    public static (DateOnly Date, List<CurrencyRate> Rates, bool IsPaid) Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath);

        // Parse date from title line (line 0)
        var date = ParseDateFromTitle(lines[0]);

        // Check for payment confirmation
        bool isPaid = lines.Any(l => l.Contains("Paid to", StringComparison.OrdinalIgnoreCase));

        // Find header line: starts with "Country or Region"
        int headerIdx = Array.FindIndex(lines, l => l.TrimStart('"').StartsWith("Country or Region"));
        if (headerIdx == -1)
            throw new Exception($"Kunne ikke finde header i {Path.GetFileName(filePath)}");

        var header = SplitCsvLine(lines[headerIdx]);
        int idxRegion       = Array.FindIndex(header, h => h.StartsWith("Country or Region", StringComparison.OrdinalIgnoreCase));
        int idxExchangeRate = Array.FindIndex(header, h => h.Equals("Exchange Rate", StringComparison.OrdinalIgnoreCase));
        int idxEarned       = Array.FindIndex(header, h => h.Equals("Earned", StringComparison.OrdinalIgnoreCase));
        int idxProceeds     = Array.FindIndex(header, h => h.Equals("Proceeds", StringComparison.OrdinalIgnoreCase));

        if (idxRegion == -1)
            throw new Exception($"Mangler kolonne 'Country or Region' i {Path.GetFileName(filePath)}");

        // Exchange Rate column may be absent in preliminary reports; we derive it from Proceeds/Earned instead
        bool hasExchangeRateCol = idxExchangeRate != -1;
        bool canDeriveRate      = idxEarned != -1 && idxProceeds != -1;

        if (!hasExchangeRateCol && !canDeriveRate)
            throw new Exception($"Mangler 'Exchange Rate' eller 'Earned'+'Proceeds' kolonner i {Path.GetFileName(filePath)}");

        var rates = new List<CurrencyRate>();

        foreach (var line in lines.Skip(headerIdx + 1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = SplitCsvLine(line);

            var region = idxRegion < cols.Length ? cols[idxRegion].Trim() : "";
            if (string.IsNullOrEmpty(region)) continue;

            decimal rate;
            if (hasExchangeRateCol && idxExchangeRate < cols.Length)
            {
                if (!decimal.TryParse(cols[idxExchangeRate].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out rate))
                    continue;
            }
            else if (canDeriveRate && idxEarned < cols.Length && idxProceeds < cols.Length)
            {
                // Derive: Proceeds (DKK) / Earned (local currency)
                if (!decimal.TryParse(cols[idxEarned].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var earned) || earned == 0)
                    continue;
                if (!decimal.TryParse(cols[idxProceeds].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var proceeds))
                    continue;
                rate = proceeds / earned;
            }
            else continue;

            // Look up currency code from region label
            if (!RegionToCurrency.TryGetValue(region, out var code)) continue;
            if (!Enum.TryParse<Currency>(code, out var currency)) continue;

            rates.Add(new CurrencyRate { Currency = currency, Rate = rate, Date = date });
        }

        // Ensure DKK is always present with rate 1
        if (!rates.Any(r => r.Currency == Currency.DKK))
            rates.Add(new CurrencyRate { Currency = Currency.DKK, Rate = 1m, Date = date });

        return (date, rates, isPaid);
    }

    private static DateOnly ParseDateFromTitle(string titleLine)
    {
        // Title: "iTunes Connect - Payments and Financial Reports	(March, 2026)"
        // Find the part inside parentheses
        var start = titleLine.IndexOf('(');
        var end   = titleLine.IndexOf(')');
        if (start == -1 || end == -1 || end <= start)
            throw new Exception($"Kunne ikke finde dato i titlen: {titleLine}");

        var datePart = titleLine.Substring(start + 1, end - start - 1).Trim(); // "March, 2026"
        var parts = datePart.Split(',');
        if (parts.Length < 2)
            throw new Exception($"Uventet datoformat i titlen: {datePart}");

        var monthName = parts[0].Trim();
        var year      = int.Parse(parts[1].Trim());

        if (!MonthNames.TryGetValue(monthName, out var month))
            throw new Exception($"Ukendt måned: {monthName}");

        return new DateOnly(year, month, 1);
    }

    private static string[] SplitCsvLine(string line)
    {
        // Simple CSV split that handles quoted fields
        var result = new List<string>();
        bool inQuotes = false;
        var field = new System.Text.StringBuilder();

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }
        result.Add(field.ToString());
        return result.ToArray();
    }
}
