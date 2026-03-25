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
        public string Month { get; set; } = "";

        public decimal Revenue { get; set; }
        public decimal GoogleFee { get; set; }
        public decimal NetPayout { get; set; }

        // NEW 👇
        //public decimal ReverseChargeNet => Math.Round(ReverseChargeBase / 1.25m, 2);
        public decimal ReverseChargeBase { get; set; }   // = GoogleFee
        public decimal ReverseChargeVAT { get; set; }    // = GoogleFee * 0.25m
    }
}
