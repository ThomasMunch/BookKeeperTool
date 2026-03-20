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
        public decimal Fee { get; set; }
        public decimal Net { get; set; }
    }
}
