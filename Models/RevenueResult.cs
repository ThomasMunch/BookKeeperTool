using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BookKeeperTool.Models
{
    public class RevenueResult
    {
        public string Source { get; set; } = "";
        public string YearMonth { get; set; } = "";
        public decimal Revenue { get; set; }
        public decimal GoogleOrAppleFee { get; set; }
        public decimal NetPayout { get; set; }
        public decimal ReverseChargeBase { get; set; }   // = GoogleOrAppleFee
        public decimal ReverseChargeVAT { get; set; }    // = GoogleOrAppleFee * 0.25m
    }
}
