using System.Collections.Generic;
using System.Threading.Tasks;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.BlockchainWallets.Contract.Models;
using Lykke.Tools.UserWalletBalanceReport.Repositories;

namespace Lykke.Tools.UserWalletBalanceReport.Services
{
    public interface IBalanceReader
    {
        Task<IEnumerable<(string address, decimal amount, string assetId)>> ReadBalance(IEnumerable<Asset> assets, string address);
        IEnumerable<string> GetAddresses(IPrivateWallet wallet);
        IEnumerable<string> GetAddresses(IWalletCredentials wallet);
        IEnumerable<string> GetAddresses(IBcnCredentialsRecord wallet);
        IEnumerable<string> GetAddresses(BlockchainWalletResponse wallet);
        IEnumerable<string> SelectUniqueAddresses(IEnumerable<string> source);
        IEnumerable<Asset> SelectRelatedAssets(IEnumerable<Asset> source);
    }
}
