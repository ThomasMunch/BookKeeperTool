using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BookKeeperTool.Models
{
    public class CurrencyRate
    {
        public Currency Currency { get; set; }
        public decimal Rate { get; set; } = 0;
        public DateOnly Date { get; set; }
    }
}
