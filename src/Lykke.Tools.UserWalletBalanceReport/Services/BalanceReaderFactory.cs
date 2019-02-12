using Lykke.Service.Assets.Client;
using Lykke.Tools.UserWalletBalanceReport.Services.Implementations;
using Lykke.Tools.UserWalletBalanceReport.Settings;
using System.Collections.Generic;
using Lykke.Tools.UserWalletBalanceReport.Services.Implementations.Bitcoin;
using Lykke.Tools.UserWalletBalanceReport.Services.Implementations.Ethereum;

namespace Lykke.Tools.UserWalletBalanceReport.Services
{
    public static class BalanceReaderFactory
    {
        public static IEnumerable<IBalanceReader> GetBalanceReaders(
            IAssetsService assetsService,
            ToolSettings toolSettings)
        {
            if (toolSettings.Bitcoin != null)
            {
                yield return BitcoinBalanceReader.Create(toolSettings);
            }

            if (toolSettings.Ethereum != null)
            {
                yield return EthereumBalanceReader.Create(toolSettings, assetsService);
            }
        }
    }
}
