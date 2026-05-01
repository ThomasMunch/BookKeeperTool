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

        // Bogførbar omsætning ekskl. kundemoms/skatter
        public decimal Revenue { get; set; }

        // Negativt beløb: faktisk Google/Apple commission
        public decimal GoogleOrAppleFee { get; set; }

        // Netto payout / Partner Share
        public decimal NetPayout { get; set; }

        public decimal ReverseChargeBase { get; set; }
        public decimal ReverseChargeVAT { get; set; }

        // Analysefelter – især Apple
        public decimal GrossCustomerPayments { get; set; }   // Kundebetaling inkl. moms/skatter
        public decimal CustomerTax { get; set; }             // Kundemoms/skatter håndteret af Apple
        public decimal AppleCommission { get; set; }         // Positivt beløb
    }
}
