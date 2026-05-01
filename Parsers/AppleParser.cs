using System.Globalization;
using BookKeeperTool.Models;

namespace BookKeeperTool.Parsers;

public class AppleParser : IParser
{
    private static readonly decimal[] PossibleAppleCommissionRates =
    {
        0.15m,
        0.30m
    };

    // Udvid efterhånden hvis du får salg i nye lande.
    // Bruges kun til at vælge korrekt split mellem 15/30 og kundemoms.
    private static readonly Dictionary<string, decimal> ExpectedTaxRates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DK"] = 0.25m,
        ["NL"] = 0.21m,
        ["SE"] = 0.25m,
        ["NO"] = 0.25m,
        ["GB"] = 0.20m,
        ["US"] = 0.00m
    };

    public string GetYearMonthFromFileName(string fileName)
    {
        var parts = fileName.Split('_');
        var raw = parts.Last(); // fx "0126"

        var monthPart = raw.Substring(0, 2);
        var yearPart = raw.Substring(2, 2);

        return $"20{yearPart}-{monthPart}";
    }

    public DateOnly GetPayoutDateFromFileName(string fileName)
    {
        var parts = fileName.Split('_');
        var raw = parts.Last(); // fx "0326"

        var monthPart = raw.Substring(0, 2);
        var yearPart = raw.Substring(2, 2);

        var year = 2000 + int.Parse(yearPart);
        var month = int.Parse(monthPart);

        var payoutDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        int daysAfterPeriodForPayout = 33;
        return payoutDate.AddDays(daysAfterPeriodForPayout);
    }

    public RevenueResult Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath);

        var headerLineIndex = lines.ToList().FindIndex(l => l.StartsWith("Transaction Date"));

        if (headerLineIndex == -1)
            throw new Exception("Kunne ikke finde header i Apple fil");

        var header = lines[headerLineIndex].Split('\t');

        int quantityIndex = RequiredIndex(header, "Quantity");
        int partnerShareIndex = RequiredIndex(header, "Partner Share");
        int extendedPartnerShareIndex = RequiredIndex(header, "Extended Partner Share");
        int partnerShareCurrencyIndex = RequiredIndex(header, "Partner Share Currency");
        int customerPriceIndex = RequiredIndex(header, "Customer Price");
        int customerCurrencyIndex = RequiredIndex(header, "Customer Currency");
        int countryIndex = RequiredIndex(header, "Country of Sale");
        int saleOrReturnIndex = RequiredIndex(header, "Sale or Return");

        decimal totalGrossCustomerPaymentsDKK = 0m;
        decimal totalCustomerTaxDKK = 0m;
        decimal totalRevenueExTaxDKK = 0m;
        decimal totalAppleCommissionDKK = 0m;
        decimal totalNetPayoutDKK = 0m;

        foreach (var line in lines.Skip(headerLineIndex + 1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var cols = line.Split('\t');

            // Stop ved summary section
            if (cols[0].Equals("Country Of Sale", StringComparison.OrdinalIgnoreCase) ||
                cols[0].Equals("Country of Sale", StringComparison.OrdinalIgnoreCase))
                break;

            if (cols.Length <= header.Length - 1)
                continue;

            try
            {
                int quantity = int.Parse(cols[quantityIndex], CultureInfo.InvariantCulture);

                string saleOrReturn = cols[saleOrReturnIndex];
                bool isReturn = saleOrReturn.Equals("R", StringComparison.OrdinalIgnoreCase);

                if (isReturn && quantity > 0)
                    quantity *= -1;

                string country = cols[countryIndex];

                decimal customerPrice = decimal.Parse(cols[customerPriceIndex], CultureInfo.InvariantCulture);
                string customerCurrency = cols[customerCurrencyIndex];

                decimal extendedPartnerShare = decimal.Parse(cols[extendedPartnerShareIndex], CultureInfo.InvariantCulture);
                string partnerShareCurrency = cols[partnerShareCurrencyIndex];

                if (isReturn && extendedPartnerShare > 0)
                    extendedPartnerShare *= -1;

                decimal grossCustomerPaymentDKK =
                    quantity * customerPrice * GetRate(customerCurrency);

                decimal netPayoutDKK =
                    extendedPartnerShare * GetRate(partnerShareCurrency);

                var split = SplitAppleAmounts(
                    grossCustomerPaymentDKK,
                    netPayoutDKK,
                    country
                );

                decimal revenueExTaxDKK = grossCustomerPaymentDKK - split.CustomerTax;

                totalGrossCustomerPaymentsDKK += grossCustomerPaymentDKK;
                totalCustomerTaxDKK += split.CustomerTax;
                totalRevenueExTaxDKK += revenueExTaxDKK;
                totalAppleCommissionDKK += split.AppleCommission;
                totalNetPayoutDKK += netPayoutDKK;
            }
            catch
            {
                continue;
            }
        }

        return new RevenueResult
        {
            Source = "Apple",

            // Bogførbar omsætning
            Revenue = Math.Round(totalRevenueExTaxDKK, 2),

            // Faktisk Apple commission som negativ udgift
            GoogleOrAppleFee = Math.Round(-totalAppleCommissionDKK, 2),

            // Netto tilgodehavende / payout
            NetPayout = Math.Round(totalNetPayoutDKK, 2),

            GrossCustomerPayments = Math.Round(totalGrossCustomerPaymentsDKK, 2),
            CustomerTax = Math.Round(totalCustomerTaxDKK, 2),
            AppleCommission = Math.Round(totalAppleCommissionDKK, 2),

            // Apple fee = ingen reverse charge i dit setup
            ReverseChargeBase = 0,
            ReverseChargeVAT = 0
        };
    }

    private static int RequiredIndex(string[] header, string columnName)
    {
        int index = Array.IndexOf(header, columnName);

        if (index == -1)
            throw new Exception($"Mangler kolonne i Apple report: {columnName}");

        return index;
    }

    private static (decimal CustomerTax, decimal AppleCommission, decimal CommissionRate, decimal TaxRate)
        SplitAppleAmounts(decimal grossCustomerPaymentDKK, decimal netPayoutDKK, string country)
    {
        if (grossCustomerPaymentDKK == 0m && netPayoutDKK == 0m)
            return (0m, 0m, 0m, 0m);

        decimal sign = grossCustomerPaymentDKK < 0m || netPayoutDKK < 0m
            ? -1m
            : 1m;

        decimal gross = Math.Abs(grossCustomerPaymentDKK);
        decimal net = Math.Abs(netPayoutDKK);

        var candidates = new List<(decimal Tax, decimal Commission, decimal CommissionRate, decimal TaxRate)>();

        foreach (var commissionRate in PossibleAppleCommissionRates)
        {
            decimal revenueExTax = net / (1m - commissionRate);
            decimal tax = gross - revenueExTax;

            // Små negative differencer kan være afrunding/valuta
            if (tax < -0.005m)
                continue;

            if (tax < 0m)
                tax = 0m;

            decimal taxRate = revenueExTax == 0m
                ? 0m
                : tax / revenueExTax;

            // Apple-landeskatter over 65% er nok forkert kandidat
            if (taxRate > 0.65m)
                continue;

            decimal commission = revenueExTax - net;

            candidates.Add((tax, commission, commissionRate, taxRate));
        }

        if (candidates.Count == 0)
        {
            // Fallback: kan ikke splitte sikkert.
            // Behandl hele forskellen som Apple commission.
            decimal fallbackCommission = gross - net;

            return (
                CustomerTax: 0m,
                AppleCommission: fallbackCommission * sign,
                CommissionRate: gross == 0m ? 0m : fallbackCommission / gross,
                TaxRate: 0m
            );
        }

        (decimal Tax, decimal Commission, decimal CommissionRate, decimal TaxRate) chosen;

        if (ExpectedTaxRates.TryGetValue(country, out var expectedTaxRate))
        {
            chosen = candidates
                .OrderBy(c => Math.Abs(c.TaxRate - expectedTaxRate))
                .First();
        }
        else
        {
            // Hvis vi ikke kender landet:
            // - hvis kun én kandidat giver mening, brug den
            // - ellers vælg laveste beregnede kundeskat
            chosen = candidates.Count == 1
                ? candidates.First()
                : candidates.OrderBy(c => c.Tax).First();
        }

        return (
            CustomerTax: chosen.Tax * sign,
            AppleCommission: chosen.Commission * sign,
            CommissionRate: chosen.CommissionRate,
            TaxRate: chosen.TaxRate
        );
    }

    private decimal GetRate(string currency)
    {
        return currency switch
        {
            "DKK" => 1m,
            "EUR" => 7.45m,
            "USD" => 6.9m,
            "GBP" => 8.6m,
            "SEK" => 0.65m,

            // Tilføj disse, fordi din januar-fil har NO og BR
            // Justér gerne satserne manuelt, hvis du vil ramme bank/Apple mere præcist.
            "NOK" => 0.64m,
            "BRL" => 1.25m,

            _ => 1m
        };
    }
}