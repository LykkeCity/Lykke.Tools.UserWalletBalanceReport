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

namespace Lykke.Tools.UserWalletBalanceReport.Services.Implementations.Bitcoin
{
    public class BitcoinBalanceReader: IBalanceReader
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

            return new BitcoinBalanceReader(client);
        }

        private BitcoinBalanceReader(QBitNinjaClient client)
        {
            _client = client;
        }

        public async Task<IEnumerable<(string address, decimal amount, string assetId)>> ReadBalance(IEnumerable<Asset> assets, string address)
        {
            var btcAddress = GetAddress(address);

            BalanceSummary sum;

            try
            {
                sum = await _client.GetBalanceSummary(btcAddress);
            }
            catch (Exception e)
            {
                throw new RetryNeededException(e);
            }

            var result = new List<(string address, decimal amount, string assetId)>();

            foreach (var asset in assets.Where(p => !string.IsNullOrEmpty(p.BlockChainAssetId)))
            {
                var btcAssetId = new BitcoinAssetId(asset.BlockChainAssetId, _client.Network);
                var amount = sum.Spendable.Assets.SingleOrDefault(p => p.Asset == btcAssetId)?.Quantity ?? 0;

                result.Add((address, amount * (decimal)asset.Multiplier(), asset.Id));
            }

            if (assets.Any(p => p.Id == "BTC"))
            {
                result.Add((address, (decimal)sum.Spendable.Amount.ToUnit(MoneyUnit.BTC), "BTC"));
            }

            return result;
        }

        public IEnumerable<string> GetAddresses(IPrivateWallet wallet)
        {
            if (IsBtcAddress(wallet.WalletAddress))
            {
                yield return wallet.WalletAddress;
            }
        }

        public IEnumerable<string> GetAddresses(IWalletCredentials wallet)
        {
            if (IsBtcAddress(wallet.MultiSig))
            {
                yield return wallet.MultiSig;
            }

            if (IsBtcAddress(wallet.ColoredMultiSig))
            {
                yield return wallet.ColoredMultiSig;
            }
        }

        public IEnumerable<string> GetAddresses(IBcnCredentialsRecord wallet)
        {
            if (IsBtcAddress(wallet.Address))
            {
                yield return wallet.AssetAddress;
            }
        }

        public IEnumerable<string> GetAddresses(BlockchainWalletResponse wallet)
        {
            if (IsBtcAddress(wallet.Address))
            {
                yield return wallet.Address;
            }
        }

        public IEnumerable<string> SelectUniqueAddresses(IEnumerable<string> source)
        {
            return source.Select(GetAddress).Distinct().Select(p => p.ToString());
        }

        public  Task<IEnumerable<Asset>> SelectRelatedAssetsAsync(IEnumerable<Asset> source)
        {
            return Task.FromResult((IEnumerable<Asset>)
                source.Where(p => IsColoredAssetId(p.BlockChainAssetId) || p.Id == "BTC").ToList());
        }

        private bool IsBtcAddress(string address)
        {
            return IsUncoloredBtcAddress(address) || IsColoredBtcAddress(address);
        }

        private bool IsColoredAssetId(string assetId)
        {
            try
            {
                new BitcoinAssetId(assetId, _client.Network);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        private BitcoinAddress GetAddress(string address)
        {

            if (IsUncoloredBtcAddress(address))
            {
                return BitcoinAddress.Create(address, _client.Network);
            }

            if (IsColoredBtcAddress(address))
            {
                return new BitcoinColoredAddress(address, _client.Network).Address;
            }

            throw new ArgumentException($"Invalid address format {address}", nameof(address));
        }

        private bool IsUncoloredBtcAddress(string address)
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

        private bool IsColoredBtcAddress(string address)
        {
            try
            {
                new BitcoinColoredAddress(address, _client.Network);

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}
