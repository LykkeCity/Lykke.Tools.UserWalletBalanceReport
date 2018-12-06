﻿using System;
using System.Collections.Generic;
using System.Text;
using Lykke.SettingsReader.Attributes;

namespace Lykke.Tools.PrivateWalletBalanceReport.Settings
{
    public class ToolSettings
    {
        [Optional]
        public BitcoinSettings Bitcoin { get; set; }

        public string AssetServiceUrl { get; set; }

        [Optional]
        public string ClientAccountUrl { get; set; }

        public string AssetId { get; set; }

        [Optional]
        public Guid? ClientId { get; set; }

        public DbSettings Db { get; set; }

        public string ResultFilePath { get; set; }

        public bool IncludeZeroBalances { get; set; } = false;

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
    }
}
