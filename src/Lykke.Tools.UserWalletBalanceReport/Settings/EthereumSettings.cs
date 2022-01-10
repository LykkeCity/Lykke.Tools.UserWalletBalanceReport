using Lykke.SettingsReader.Attributes;

namespace Lykke.Tools.UserWalletBalanceReport.Settings
{
    public class EthereumSettings
    {
        [Optional]
        public string EthereumCoreUrl { get; set; }
    }
}