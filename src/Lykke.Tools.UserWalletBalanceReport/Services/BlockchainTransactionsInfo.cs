using System;

namespace Lykke.Tools.UserWalletBalanceReport.Services
{
    public class BlockchainTransactionsInfo
    {
        public string AssetId { get; set; }
        public decimal ReceivedAmount { get; set; }
        public decimal SpentAmount { get; set; }
        public int TransactionsCount { get; set; }
    }
}
