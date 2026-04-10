using BookKeeperTool.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BookKeeperTool.Parsers
{
    public interface IParser
    {
        RevenueResult Parse(string filePath);

        string GetYearMonthFromFileName(string fileName);
        //string GetExpectedPayoutMonthFromFileName(string fileName);

        DateOnly GetPayoutDateFromFileName(string fileName);
    }
}
