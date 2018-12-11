using System;
using Lykke.Service.Assets.Client.Models;
using Lykke.Tools.PrivateWalletBalanceReport.Services.Implementations;
using Lykke.Tools.PrivateWalletBalanceReport.Settings;

namespace Lykke.Tools.PrivateWalletBalanceReport.Services
{
    public static class BalanceReaderFactory
    {
        public static IBalanceReader GetBalanceReader(Asset asset, ToolSettings toolSettings)
        {
            switch (asset.Blockchain)
            {
                case Blockchain.Bitcoin:
                {
                    return BitcoinBalanceReader.Create(toolSettings);
                }
                default:
                {
                    throw new ArgumentException($"Balance reader for blockchain {asset.Blockchain} not implemented");
                }
            }
        }
    }
}
