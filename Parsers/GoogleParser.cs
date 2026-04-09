using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using BookKeeperTool.Models;

namespace BookKeeperTool.Parsers
{
    public class GoogleParser: IParser
    {
            
        public string GetMonthFromFileName(string fileName)
        {
            var month = fileName.Split('_')[0];//Google
            return month;
        }

        /// <summary>
        /// Google udbetaler ca. 16 dage efter månedsafslutning
        /// </summary>
        /// <param name="fileName">Filnavnet, fx "2025-12_PlayApps.csv"</param>
        /// <returns></returns>
        public DateOnly GetPayoutDateFromFileName(string fileName)
        {
            var yearAndMonth = fileName.Split('_')[0];//Google

            var parts = yearAndMonth.Split('-');
          
            var monthPart = parts.Last(); // fx "12"
            var yearPart = parts.First(); // fx "2025"

            var year = int.Parse(yearPart);
            var month = int.Parse(monthPart);

            DateOnly payoutDate = new DateOnly(year, month, 1);
            int daysInMonthMinus1 = DateTime.DaysInMonth(year, month) - 1;
            payoutDate = payoutDate.AddDays(daysInMonthMinus1);

            int daysAfterPeriodForPayout = 16; // Google udbetaler ca. 16 dage efter månedsafslutning
            return payoutDate.AddDays(daysAfterPeriodForPayout);
        }


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
            var feeAbs = Math.Abs(fee);
            return new RevenueResult
            {
                Source = "Google",
                Revenue = revenue,
                GoogleOrAppleFee = fee,
                NetPayout = revenue + fee,
                ReverseChargeBase = feeAbs,
                ReverseChargeVAT = Math.Round(feeAbs * 0.25m, 2)
            };



        }
    }
}