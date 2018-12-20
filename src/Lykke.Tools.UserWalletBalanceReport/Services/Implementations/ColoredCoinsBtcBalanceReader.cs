using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.Assets.Client.Models.Extensions;
using Lykke.Service.BlockchainWallets.Contract.Models;
using Lykke.Tools.UserWalletBalanceReport.Repositories;
using Lykke.Tools.UserWalletBalanceReport.Settings;
using NBitcoin;
using NBitcoin.OpenAsset;
using QBitNinja.Client;
using QBitNinja.Client.Models;

namespace Lykke.Tools.UserWalletBalanceReport.Services.Implementations
{
    public class ColoredCoinsBtcBalanceReader: IBalanceReader
    {
        private readonly QBitNinjaClient _client;
        
        public static IBalanceReader Create(ToolSettings toolSettings)
        {
            if (toolSettings.Bitcoin?.Network == null)
            {
                throw new ArgumentNullException(nameof(toolSettings.Bitcoin.Network));
            }

            if (toolSettings.Bitcoin?.NinjaUrl == null)
            {
                throw new ArgumentNullException(nameof(toolSettings.Bitcoin.NinjaUrl));
            }

            var client = new QBitNinjaClient(new Uri(toolSettings.Bitcoin.NinjaUrl),
                Network.GetNetwork(toolSettings.Bitcoin.Network))
            {
                Colored = true
            };

            return new ColoredCoinsBtcBalanceReader(client);
        }

        private ColoredCoinsBtcBalanceReader(QBitNinjaClient client)
        {
            _client = client;
        }

        public async Task<(string address, decimal amount)> ReadBalance(Asset asset, string address)
        {
            var btcAssetId = new BitcoinAssetId(asset.BlockChainAssetId, _client.Network);

            var btcAddress = BitcoinAddress.Create(address,
                _client.Network);

            BalanceSummary sum;

            try
            {
                sum = await _client.GetBalanceSummary(btcAddress);
            }
            catch (Exception e)
            {
                throw new RetryNeededException(e);
            }

            var amount = sum.Spendable.Assets.SingleOrDefault(p => p.Asset == btcAssetId)?.Quantity ?? 0;

            return (address, amount * (decimal)asset.Multiplier());
        }

        public IEnumerable<string> GetAddresses(IPrivateWallet wallet)
        {
            yield return wallet.WalletAddress;
        }

        public IEnumerable<string> GetAddresses(IWalletCredentials wallet)
        {
            if (IsBtcAddress(wallet.Address))
            {
                yield return wallet.Address;
            }

            if (IsBtcAddress(wallet.MultiSig))
            {
                yield return wallet.MultiSig;
            }

            if (IsBtcAddress(wallet.ColoredMultiSig))
            {
                yield return wallet.ColoredMultiSig;
            }
            
            if (IsBtcAddress(wallet.BtcConvertionWalletAddress))
            {
                yield return wallet.BtcConvertionWalletAddress;
            }
        }

        public IEnumerable<string> GetAddresses(IBcnCredentialsRecord wallet)
        {
            if (IsBtcAddress(wallet.Address))
            {
                yield return wallet.Address;
            }
        }

        public IEnumerable<string> GetAddresses(AddressResponse wallet)
        {
            yield return wallet.Address;
        }

        private bool IsBtcAddress(string address)
        {
            try
            {
                BitcoinAddress.Create(address,
                    _client.Network);

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}
