using System.Globalization;
using BookKeeperTool.Models;

namespace BookKeeperTool.Parsers;

public class AppleParser : IParser
{
  
    public string GetMonthFromFileName(string fileName)
    {
        var parts = fileName.Split('_');
        var raw = parts.Last(); // fx "1125"

        // MMYY → yyyy-MM
        var monthPart = raw.Substring(0, 2);
        var yearPart = raw.Substring(2, 2);

        var month = $"20{yearPart}-{monthPart}";
        return month;
    }

    public DateOnly GetPayoutDateFromFileName(string fileName)
    {
        var parts = fileName.Split('_');
        var raw = parts.Last(); // fx "1125"

        // MMYY → yyyy-MM
        var monthPart = raw.Substring(0, 2);
        var yearPart = raw.Substring(2, 2);

        var year = 2000 + int.Parse(yearPart);
        var month = int.Parse(monthPart);

        DateOnly payoutDate = new DateOnly(year, month, 1);
        int daysInMonthMinus1 = DateTime.DaysInMonth(year, month) - 1;
        payoutDate = payoutDate.AddDays(daysInMonthMinus1);

        int daysAfterPeriodForPayout = 33; // Apple udbetaler ca. 33 dage efter månedsafslutning
        return payoutDate.AddDays(daysAfterPeriodForPayout);
    }

    public RevenueResult Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath);

        var headerLineIndex = lines.ToList().FindIndex(l => l.StartsWith("Transaction Date"));

        if (headerLineIndex == -1)
            throw new Exception("Kunne ikke finde header i Apple fil");

        var header = lines[headerLineIndex].Split('\t');

        int quantityIndex = Array.IndexOf(header, "Quantity");
        int partnerShareIndex = Array.IndexOf(header, "Partner Share");
        int customerPriceIndex = Array.IndexOf(header, "Customer Price");
        int currencyIndex = Array.IndexOf(header, "Partner Share Currency");

        decimal totalRevenueDKK = 0m;
        decimal totalNetDKK = 0m;

        foreach (var line in lines.Skip(headerLineIndex + 1)) //Was: foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var cols = line.Split('\t');

            // Stop ved summary section
            if (cols[0] == "Country Of Sale")
                break;

            if (cols.Length <= Math.Max(Math.Max(quantityIndex, partnerShareIndex), customerPriceIndex))
                continue;

            try
            {
                int quantity = int.Parse(cols[quantityIndex]);

                decimal partnerShare = decimal.Parse(cols[partnerShareIndex], CultureInfo.InvariantCulture);
                decimal customerPrice = decimal.Parse(cols[customerPriceIndex], CultureInfo.InvariantCulture);

                string currency = cols[currencyIndex];

                decimal rate = GetRate(currency);

                totalRevenueDKK += quantity * customerPrice * rate;
                totalNetDKK += quantity * partnerShare * rate;
            }
            catch
            {
                continue;
            }
        }

        //var fee = totalRevenueDKK - totalNetDKK;


        //return new RevenueResult
        //{
        //    Revenue = Math.Round(totalNetDKK, 2),
        //    GoogleOrAppleFee = 0,
        //    NetPayout = Math.Round(totalNetDKK, 2),
        //    ReverseChargeBase = 0,
        //    ReverseChargeVAT = 0
        //};
        var fee = totalRevenueDKK - totalNetDKK;

        return new RevenueResult
        {
            Source = "Apple",

            // 👇 VIS BRUTTO (til analyse)
            Revenue = Math.Round(totalRevenueDKK, 2),

            // 👇 VIS FEE (til analyse)
            GoogleOrAppleFee = Math.Round(-fee, 2),

            // 👇 DETTE ER DET ENESTE DU BRUGER TIL BOGFØRING
            NetPayout = Math.Round(totalNetDKK, 2),

            // 👇 STADIG 0 (ingen reverse charge!)
            ReverseChargeBase = 0,
            ReverseChargeVAT = 0
        };

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
            _ => 1m
        };
    }
}