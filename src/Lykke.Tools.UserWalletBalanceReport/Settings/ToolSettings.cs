using System;
using Lykke.SettingsReader.Attributes;

namespace Lykke.Tools.UserWalletBalanceReport.Settings
{
    public class ToolSettings
    {
        [Optional]
        public BitcoinSettings Bitcoin { get; set; }

        [Optional]
        public EthereumSettings Ethereum { get; set; }

        public WalletTypes WalletType { get; set; }

        public string AssetServiceUrl { get; set; }

        [Optional]
        public string ClientAccountUrl { get; set; }

        [Optional]
        public string BlockchainWalletsUrl { get; set; }

        [Optional]
        public string AssetId { get; set; }

        public DbSettings Db { get; set; }

        public string ResultFilePath { get; set; }

        public string ErrorFilePath { get; set; }

        [Optional]
        public bool IncludeZeroBalances { get; set; }

        [Optional]
        public string ClientIdsFilePath { get; set; }

        [Optional]
        public PrivateWalletsCountSettings PrivateWalletsCount { get; set; }
    }

    public class PrivateWalletsCountSettings
    {
        public DateTime? FromDate { get; set; }
        public int FromBlock { get; set; }
    }
}
