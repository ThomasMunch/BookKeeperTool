using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using BookKeeperTool.Models;

namespace BookKeeperTool.Parsers
{
    public class GoogleParser
    {
        public RevenueResult Parse(string filePath)
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                BadDataFound = null,
                MissingFieldFound = null,
                HeaderValidated = null
            });

            var records = csv.GetRecords<dynamic>();

            decimal revenue = 0;
            decimal fee = 0;

            foreach (var record in records)
            {
                var dict = (IDictionary<string, object>)record;

                var type = dict["Transaction Type"]?.ToString();
                var amountStr = dict["Amount (Merchant Currency)"]?.ToString();

                if (decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                {
                    if (type == "Charge")
                        revenue += amount;

                    else if (type == "Google fee")
                        fee += amount;
                }
            }

            return new RevenueResult
            {
                Source = "Google",
                Revenue = revenue,
                Fee = fee,
                Net = revenue + fee
            };
        }
    }
}