﻿using Lykke.SettingsReader.Attributes;

namespace Lykke.Tools.UserWalletBalanceReport.Settings
{
    public class ToolSettings
    {
        [Optional]
        public BitcoinSettings Bitcoin { get; set; }

        public WalletTypes WalletType { get; set; }

        public string AssetServiceUrl { get; set; }

        [Optional]
        public string ClientAccountUrl { get; set; }

        [Optional]
        public string BlockchainWalletsUrl { get; set; }

        public string AssetId { get; set; }
        
        public DbSettings Db { get; set; }

        public string ResultFilePath { get; set; }

        public string ErrorFilePath { get; set; }

        [Optional]
        public bool IncludeZeroBalances { get; set; }

        [Optional]
        public string ClientIdsFilePath { get; set; }
        
        public class BitcoinSettings
        {
            [Optional]
            public string Network { get; set; }

            [Optional]
            public string NinjaUrl { get; set; }
        }

        public class DbSettings
        {
            public string ClientPersonalInfoConnString { get; set; }
        }

        public enum WalletTypes
        {
            Private,
            Deposit
        }
    }
}
