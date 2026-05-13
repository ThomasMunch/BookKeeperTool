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

        // Shared fields
        public decimal Revenue { get; set; }               // Bogførbar omsætning ekskl. kundemoms/skatter
        public decimal GoogleOrAppleFee { get; set; }      // Negativt fee (for kompatibilitet)
        public decimal NetPayout { get; set; }             // Netto til udbetaling i DKK
        public decimal ReverseChargeBase { get; set; }
        public decimal ReverseChargeVAT { get; set; }

        // Apple-specific fields
        public decimal GrossCustomerPayments { get; set; } // Kundebetaling inkl. Apple-opkrævede skatter
        public decimal CustomerTax { get; set; }           // Apple-opkrævede kundemoms/skatter
        public decimal AppleCommission { get; set; }       // Apple commission (positivt beløb)
        public bool UsedEstimatedExchangeRates { get; set; }
        public string ExchangeRateSourceDescription { get; set; } = "";
        public string SettlementStatus { get; set; } = "";
        public List<string> Warnings { get; set; } = new();
    }
}
