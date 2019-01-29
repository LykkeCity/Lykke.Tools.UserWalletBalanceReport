using System.Collections.Generic;
using Lykke.Service.Assets.Client.Models;
using Lykke.Tools.UserWalletBalanceReport.Services.Implementations;
using Lykke.Tools.UserWalletBalanceReport.Settings;

namespace Lykke.Tools.UserWalletBalanceReport.Services
{
    public static class BalanceReaderFactory
    {
        public static IEnumerable<IBalanceReader> GetBalanceReaders(IEnumerable<Asset> assets, ToolSettings toolSettings)
        {
            if (toolSettings.Bitcoin != null)
            {
                yield return BitcoinBalanceReader.Create(toolSettings);
            }
        }
    }
}
