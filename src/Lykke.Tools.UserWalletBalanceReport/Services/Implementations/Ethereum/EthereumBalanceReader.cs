using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.BlockchainWallets.Contract.Models;
using Lykke.Service.EthereumCore.Client;
using Lykke.Service.EthereumCore.Client.Models;
using Lykke.Tools.UserWalletBalanceReport.Repositories;
using Lykke.Tools.UserWalletBalanceReport.Settings;
using Nethereum.Util;

namespace Lykke.Tools.UserWalletBalanceReport.Services.Implementations.Ethereum
{
    public class EthereumBalanceReader : IBalanceReader
    {
        private readonly static Regex _ethAddressIgnoreCaseRegex = new Regex("^(0x)?[0-9a-f]{40}$", RegexOptions.Compiled
                                                                                                    | RegexOptions.IgnoreCase);
        private readonly static Regex _ethAddressRegex = new Regex("(0x)?[0-9a-f]{40}$", RegexOptions.Compiled);
        private readonly static Regex _ethAddressCapitalRegex = new Regex("^(0x)?[0-9A-F]{40}$", RegexOptions.Compiled);

        private readonly EthereumCoreAPI _client;
        private readonly IAssetsService _assetsService;
        private readonly AddressUtil _addressUtil;
        private readonly Lazy<Task<IEnumerable<Asset>>> _lazyErc20AssetsCache;

        public static IBalanceReader Create(ToolSettings toolSettings, IAssetsService assetsService)
        {
            if (toolSettings.Ethereum?.EthereumCoreUrl == null)
            {
                throw new ArgumentNullException(nameof(toolSettings.Ethereum.EthereumCoreUrl));
            }

            var client = new EthereumCoreAPI(new Uri(toolSettings.Ethereum.EthereumCoreUrl));

            return new EthereumBalanceReader(client, assetsService);
        }

        private EthereumBalanceReader(EthereumCoreAPI client, IAssetsService assetsService)
        {
            _lazyErc20AssetsCache = new Lazy<Task<IEnumerable<Asset>>>(async () =>
            {
                var result = await GetApprovedTokenAssets(null);

                return result;
            });
            _assetsService = assetsService ?? throw new ArgumentNullException(nameof(assetsService));
            _addressUtil = new AddressUtil();
            _client = client;
        }

        public async Task<IEnumerable<(string address, decimal amount, string assetId)>> ReadBalance(IEnumerable<Asset> assets, string address)
        {
            object response;
            try
            {
                response = await _client.ApiRpcGetBalanceByAddressGetAsync(address);
            }
            catch (Exception e)
            {
                throw new RetryNeededException(e);
            }

            var result = new List<(string address, decimal amount, string assetId)>();

            var ethAsset = GetEthAsset(assets);

            if (response is ApiException error)
            {
                throw new RetryNeededException(new Exception(error.Error.Message + " " + error.Error.Code));
            }

            var erc20Asset = new Dictionary<string, Asset>();
            var tokens = await _assetsService.Erc20TokenGetBySpecificationAsync(new Lykke.Service.Assets.Client.Models.Erc20TokenSpecification()
            {
                Ids = assets?.Where(x => x.Blockchain == Blockchain.Ethereum).Select(x => x.Id).ToList()
            });

            var tokenAddresses = assets.Join(tokens?.Items, x => x.Id, y => y.AssetId, (asset, token) =>
            {
                string assetAddress = token?.Address?.ToLower();
                if (!string.IsNullOrEmpty(assetAddress))
                    erc20Asset[assetAddress] = asset;

                return assetAddress;
            })?.Where(x => x != null).ToList();

            var tokenResponse = await _client.ApiErc20BalancePostAsync(new GetErcBalance(address, tokenAddresses));
            error = tokenResponse as ApiException;
            if (error != null)
            {
                throw new RetryNeededException(new Exception(error.Error.Message + " " + error.Error.Code));
            }

            var tokenBalances = tokenResponse as AddressTokenBalanceContainerResponse;

            if (tokenBalances != null)
            {
                var tokenBalancesCalculated = tokenBalances.Balances.Select(x =>
                {
                    string ercAddress = x.Erc20TokenAddress.ToLower();
                    Asset asset = null;

                    erc20Asset.TryGetValue(ercAddress, out asset);

                    if (asset == null)
                        return (null, 0, null);

                    return (
                        address,
                        EthServiceHelpers.ConvertFromContract(x.Balance, asset.MultiplierPower,
                                                   asset.Accuracy),
                        asset.Id);
                })?.Where(x => x.Item1 != null).ToList();

                if (tokenBalancesCalculated.Count > 0)
                {
                    result.AddRange(tokenBalancesCalculated);
                }
            }

            var res = response as BalanceModel;
            if (res != null)
            {
                result.Add((address,
                            EthServiceHelpers.ConvertFromContract(res.Amount,
                                ethAsset.MultiplierPower,
                                ethAsset.Accuracy),
                            ethAsset.Id));
            }

            return result;
        }

        public IEnumerable<string> GetAddresses(IPrivateWallet wallet)
        {
            if (IsValidAddress(wallet.WalletAddress))
            {
                yield return wallet.WalletAddress;
            }
        }

        public IEnumerable<string> GetAddresses(IWalletCredentials wallet)
        {
            if (IsValidAddress(wallet.EthAddress))
            {
                yield return wallet.MultiSig;
            }
        }

        public IEnumerable<string> GetAddresses(IBcnCredentialsRecord wallet)
        {
            if (IsValidAddress(wallet.Address))
            {
                yield return wallet.AssetAddress;
            }
        }

        public IEnumerable<string> GetAddresses(BlockchainWalletResponse wallet)
        {
            if (IsValidAddress(wallet.Address))
            {
                yield return wallet.Address;
            }
        }

        public IEnumerable<string> SelectUniqueAddresses(IEnumerable<string> source)
        {
            return source.Distinct().Select(p => p.ToString());
        }

        public async Task<IEnumerable<Asset>> SelectRelatedAssetsAsync(IEnumerable<Asset> source)
        {
            return await _lazyErc20AssetsCache.Value;
        }

        private async Task<IEnumerable<Asset>> GetApprovedTokenAssets(IEnumerable<Asset> source)
        {
            source = source != null ? source : await _assetsService.AssetGetAllAsync();
            var dict = source.ToDictionary(x => x.Id);
            var ethAsset = GetEthAsset(source);
            var tokens = await _assetsService.Erc20TokenGetBySpecificationAsync(
                new Lykke.Service.Assets.Client.Models.Erc20TokenSpecification()
                {
                    Ids = source?.Where(x => x.Blockchain == Blockchain.Ethereum).Select(x => x.Id).ToList()
                });

            var approvedTokens = tokens.Items.Select(x =>
            {
                dict.TryGetValue(x.AssetId, out var asset);
                return asset;
            }).Where(x => x != null).ToList();

            if (ethAsset != null)
                approvedTokens.Add(ethAsset);

            return approvedTokens.ToList();
        }

        private Asset GetEthAsset(IEnumerable<Asset> assets)
        {
            var ethAsset = assets.FirstOrDefault(x => x.BlockChainAssetId == "ETH");

            return ethAsset;
        }

        public bool IsValidAddress(string address)
        {
            if (!_ethAddressIgnoreCaseRegex.IsMatch(address))
            {
                // check if it has the basic requirements of an address
                return false;
            }
            else if (_ethAddressRegex.IsMatch(address) ||
                     _ethAddressCapitalRegex.IsMatch(address))
            {
                // If it's all small caps or all all caps, return true
                return true;
            }
            else
            {
                // Check each case
                return _addressUtil.IsChecksumAddress(address);
            };
        }
    }
}
