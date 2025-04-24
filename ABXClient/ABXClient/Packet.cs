using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ABXClient
{
    public class Packet
    {
        public uint Sequence { get; set; } // Packet number (e.g., 1, 2, 3)
        public string Symbol { get; set; } // Stock symbol (e.g., "AAPL")
        public char BuySell { get; set; } // 'B' for Buy, 'S' for Sell
        public uint Quantity { get; set; } // Number of shares
        public uint Price { get; set; } // Price in cents
    }

}
