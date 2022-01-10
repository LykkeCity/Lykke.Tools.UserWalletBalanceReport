using Lykke.SettingsReader.Attributes;

namespace Lykke.Tools.UserWalletBalanceReport.Settings
{
    public class BitcoinSettings
    {
        [Optional]
        public string Network { get; set; }

        [Optional]
        public string NinjaUrl { get; set; }
    }
}