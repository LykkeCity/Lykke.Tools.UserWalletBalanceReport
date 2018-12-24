using System.Collections.Generic;
using System.Threading.Tasks;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.BlockchainWallets.Contract.Models;
using Lykke.Tools.UserWalletBalanceReport.Repositories;

namespace Lykke.Tools.UserWalletBalanceReport.Services
{
    public interface IBalanceReader
    {
        Task<(string address, decimal amount)> ReadBalance(Asset asset, string address);
        IEnumerable<string> GetAddresses(IPrivateWallet wallet);
        IEnumerable<string> GetAddresses(IWalletCredentials wallet);
        IEnumerable<string> GetAddresses(IBcnCredentialsRecord wallet);
        IEnumerable<string> GetAddresses(AddressResponse wallet);
    }
}
