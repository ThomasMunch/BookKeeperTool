using System.Globalization;
using BookKeeperTool.Models;

namespace BookKeeperTool.Parsers;

public class AppleParser : IParser
{
    // Expected VAT rates per Apple country-of-sale code
    private static readonly Dictionary<string, decimal> ExpectedTaxRates = new()
    {
        ["DK"] = 0.25m,
        ["NL"] = 0.21m,
        ["SE"] = 0.25m,
        ["NO"] = 0.25m,
        ["GB"] = 0.20m,
        ["DE"] = 0.19m,
        ["FR"] = 0.20m,
        ["ES"] = 0.21m,
        ["IT"] = 0.22m,
        ["US"] = 0.00m,
        ["CA"] = 0.05m,
        ["AU"] = 0.10m,
        ["JP"] = 0.10m,
        ["BR"] = 0.00m,  // Apple handles Brazilian taxes separately
        ["BE"] = 0.21m,
    };

    // Apple commission candidates: Small Business (15%) and standard (30%)
    private static readonly decimal[] CommissionCandidates = [0.15m, 0.30m];

    public string GetYearMonthFromFileName(string fileName)
    {
        var parts = fileName.Split('_');
        var raw = parts.Last(); // fx "0326"
        var monthPart = raw.Substring(0, 2);
        var yearPart = raw.Substring(2, 2);
        return $"20{yearPart}-{monthPart}";
    }

    /// <summary>
    /// Apple udbetaler ca. 33 dage efter månedsafslutning
    /// </summary>
    public DateOnly GetPayoutDateFromFileName(string fileName)
    {
        var parts = fileName.Split('_');
        var raw = parts.Last(); // fx "0326"
        var monthPart = raw.Substring(0, 2);
        var yearPart = raw.Substring(2, 2);
        var year = 2000 + int.Parse(yearPart);
        var month = int.Parse(monthPart);
        var payoutDate = new DateOnly(year, month, 1);
        payoutDate = payoutDate.AddDays(DateTime.DaysInMonth(year, month) - 1);
        return payoutDate.AddDays(33);
    }

    public RevenueResult Parse(string filePath) => Parse(filePath, null, false);

    public RevenueResult Parse(string filePath, List<CurrencyRate>? currencyRates) => Parse(filePath, currencyRates, false);

    public RevenueResult Parse(string filePath, List<CurrencyRate>? currencyRates, bool isPaid)
    {
        var warnings = new List<string>();
        var usedFallback = false;
        var rateSourceDesc = currencyRates != null
            ? $"Apple financial report ({currencyRates.FirstOrDefault()?.Date.ToString("yyyy-MM") ?? "ukendt"})"
            : "Hardkodede standardkurser (ingen financial report fundet)";

        var lines = File.ReadAllLines(filePath);
        var headerLineIndex = lines.ToList().FindIndex(l => l.StartsWith("Transaction Date"));
        if (headerLineIndex == -1)
            throw new Exception("Kunne ikke finde header i Apple fil");

        var header = lines[headerLineIndex].Split('\t');

        int idxQuantity          = Array.IndexOf(header, "Quantity");
        int idxPartnerShare      = Array.IndexOf(header, "Partner Share");
        int idxExtPartnerShare   = Array.IndexOf(header, "Extended Partner Share");
        int idxPartnerShareCurr  = Array.IndexOf(header, "Partner Share Currency");
        int idxCustomerPrice     = Array.IndexOf(header, "Customer Price");
        int idxCustomerCurr      = Array.IndexOf(header, "Customer Currency");
        int idxCountry           = Array.IndexOf(header, "Country of Sale");
        int idxSaleOrReturn      = Array.IndexOf(header, "Sale or Return");

        decimal totalGrossDkk      = 0m;
        decimal totalNetDkk        = 0m;
        decimal totalCustomerTax   = 0m;
        decimal totalRevExTax      = 0m;
        decimal totalCommission    = 0m;

        foreach (var line in lines.Skip(headerLineIndex + 1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = line.Split('\t');
            if (cols[0] == "Country Of Sale") break;

            int maxIdx = new[] { idxQuantity, idxPartnerShare, idxCustomerPrice, idxPartnerShareCurr }.Max();
            if (cols.Length <= maxIdx) continue;

            try
            {
                int quantity = int.Parse(cols[idxQuantity]);
                bool isReturn = idxSaleOrReturn >= 0
                    && cols.Length > idxSaleOrReturn
                    && cols[idxSaleOrReturn].Trim().Equals("R", StringComparison.OrdinalIgnoreCase);
                int sign = isReturn ? -1 : 1;

                // Extended Partner Share preferred over Quantity * Partner Share
                decimal extPartnerShare;
                if (idxExtPartnerShare >= 0 && cols.Length > idxExtPartnerShare
                    && decimal.TryParse(cols[idxExtPartnerShare], NumberStyles.Any, CultureInfo.InvariantCulture, out var eps))
                {
                    extPartnerShare = eps;
                }
                else
                {
                    var ps = decimal.Parse(cols[idxPartnerShare], CultureInfo.InvariantCulture);
                    extPartnerShare = quantity * ps;
                }

                decimal customerPrice = decimal.Parse(cols[idxCustomerPrice], CultureInfo.InvariantCulture);
                string partnerCurr = cols[idxPartnerShareCurr].Trim();
                string customerCurr = idxCustomerCurr >= 0 && cols.Length > idxCustomerCurr
                    ? cols[idxCustomerCurr].Trim()
                    : partnerCurr;
                string country = idxCountry >= 0 && cols.Length > idxCountry
                    ? cols[idxCountry].Trim()
                    : "";

                decimal partnerRate = GetRate(partnerCurr, currencyRates, warnings, ref usedFallback);
                decimal customerRate = customerCurr == partnerCurr
                    ? partnerRate
                    : GetRate(customerCurr, currencyRates, warnings, ref usedFallback);

                // Apply return sign to raw amounts
                decimal signedExtPartnerShare = sign * Math.Abs(extPartnerShare);
                decimal signedCustomerPrice   = sign * Math.Abs(customerPrice);

                decimal grossDkk = signedCustomerPrice * quantity * customerRate;
                decimal netDkk   = signedExtPartnerShare * partnerRate;

                // Determine commission rate by testing candidates against expected tax rate
                decimal commissionRate = PickCommissionRate(country, grossDkk, netDkk, warnings);

                // revenueExTax = |netDkk| / (1 - commissionRate)
                decimal absNet  = Math.Abs(netDkk);
                decimal absGross = Math.Abs(grossDkk);

                decimal revenueExTax    = absNet / (1m - commissionRate);
                decimal customerTax     = absGross - revenueExTax;
                decimal appleCommission = revenueExTax - absNet;

                // Restore sign
                int dkSign = netDkk < 0 ? -1 : 1;
                totalGrossDkk    += grossDkk;
                totalNetDkk      += netDkk;
                totalRevExTax    += dkSign * revenueExTax;
                totalCustomerTax += dkSign * customerTax;
                totalCommission  += dkSign * appleCommission;
            }
            catch
            {
                continue;
            }
        }

        if (usedFallback)
            warnings.Insert(0, "En eller flere valutakurser er baseret på hardkodede standardkurser (ingen kurs fundet i financial report).");

        return new RevenueResult
        {
            Source = "Apple",
            GrossCustomerPayments = Math.Round(totalGrossDkk, 2),
            CustomerTax           = Math.Round(totalCustomerTax, 2),
            Revenue               = Math.Round(totalRevExTax, 2),
            AppleCommission       = Math.Round(totalCommission, 2),
            GoogleOrAppleFee      = -Math.Round(totalCommission, 2),
            NetPayout             = Math.Round(totalNetDkk, 2),
            ReverseChargeBase     = 0,
            ReverseChargeVAT      = 0,
            UsedEstimatedExchangeRates   = currencyRates == null || usedFallback,
            ExchangeRateSourceDescription = rateSourceDesc,
            SettlementStatus      = isPaid
                ? "ENDELIG – betalt til bank"
                : "FORELØBIG – endelig Apple payment report/bankindbetaling mangler",
            Warnings              = warnings,
        };
    }

    /// <summary>
    /// Selects the Apple commission rate (0.15 or 0.30) that produces the most plausible
    /// customer tax rate for the given country.
    /// </summary>
    private static decimal PickCommissionRate(string country, decimal grossDkk, decimal netDkk, List<string> warnings)
    {
        decimal absGross = Math.Abs(grossDkk);
        decimal absNet   = Math.Abs(netDkk);

        if (absNet == 0 || absGross == 0) return 0.30m;

        ExpectedTaxRates.TryGetValue(country, out decimal expectedTax);

        decimal bestRate = 0.30m;
        decimal bestDiff = decimal.MaxValue;

        foreach (var candidate in CommissionCandidates)
        {
            if (candidate >= 1m) continue;
            decimal revEx = absNet / (1m - candidate);
            if (revEx <= 0 || revEx > absGross) continue;

            decimal impliedTax = (absGross - revEx) / absGross;
            if (impliedTax < 0 || impliedTax > 0.65m) continue;

            decimal diff = Math.Abs(impliedTax - expectedTax);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestRate = candidate;
            }
        }

        return bestRate;
    }

    private decimal GetRate(string currency, List<CurrencyRate>? rates, List<string> warnings, ref bool usedFallback)
    {
        if (rates != null && Enum.TryParse<Currency>(currency, out var curr))
        {
            var match = rates.FirstOrDefault(r => r.Currency == curr);
            if (match != null) return match.Rate;
        }

        // Fallback to hardcoded
        var fallback = GetFallbackRate(currency);
        if (currency != "DKK")
        {
            usedFallback = true;
            if (!warnings.Any(w => w.Contains(currency)))
                warnings.Add($"Ingen kurs fundet i financial report for {currency} – bruger hardkodet kurs {fallback}.");
        }
        return fallback;
    }

    private static decimal GetFallbackRate(string currency) => currency switch
    {
        "DKK" => 1m,
        "EUR" => 7.45m,
        "USD" => 6.9m,
        "GBP" => 8.6m,
        "SEK" => 0.65m,
        "NOK" => 0.63m,
        "BRL" => 1.2m,
        "CAD" => 5.1m,
        _ => 1m
    };
}
