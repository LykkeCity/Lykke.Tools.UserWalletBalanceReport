using System;
using Lykke.Service.Assets.Client.Models;
using Lykke.Tools.UserWalletBalanceReport.Services.Implementations;
using Lykke.Tools.UserWalletBalanceReport.Settings;

namespace Lykke.Tools.UserWalletBalanceReport.Services
{
    public static class BalanceReaderFactory
    {
        public static IBalanceReader GetBalanceReader(Asset asset, ToolSettings toolSettings)
        {
            switch (asset.Blockchain)
            {
                case Blockchain.Bitcoin:
                {
                    return ColoredCoinsBtcBalanceReader.Create(toolSettings);
                }
                default:
                {
                    throw new ArgumentException($"Balance reader for blockchain {asset.Blockchain} not implemented");
                }
            }
        }
    }
}
