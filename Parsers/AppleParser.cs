using System.Globalization;
using BookKeeperTool.Models;

namespace BookKeeperTool.Parsers
{
    public class AppleParser
    {
        public RevenueResult Parse(string filePath)
        {
            var lines = File.ReadAllLines(filePath);

            if (lines.Length < 2)
                throw new Exception("Tom eller ugyldig Apple rapport");

            var headers = lines[0].Split('\t');

            int amountIndex = Array.IndexOf(headers, "Extended Partner Share");
            int currencyIndex = Array.IndexOf(headers, "Partner Share Currency");
            int typeIndex = Array.IndexOf(headers, "Sale or Return");

            if (amountIndex == -1 || currencyIndex == -1 || typeIndex == -1)
                throw new Exception("Kunne ikke finde nødvendige kolonner");

            decimal total = 0;

            for (int i = 1; i < lines.Length; i++)
            {
                var cols = lines[i].Split('\t');

                if (cols.Length <= Math.Max(amountIndex, Math.Max(currencyIndex, typeIndex)))
                    continue;

                var type = cols[typeIndex];
                var currency = cols[currencyIndex];
                var amountStr = cols[amountIndex];

                // Kun rigtige salg
                if (type != "S")
                    continue;

                // Kun DKK (version 1)
                if (currency != "DKK")
                    continue;

                if (decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                {
                    total += amount;
                }
            }

            return new RevenueResult
            {
                Source = "Apple",
                Revenue = total,
                Fee = 0, // Apple fee er allerede trukket
                Net = total
            };
        }
    }
}