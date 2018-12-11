using System.Threading.Tasks;
using Lykke.Service.Assets.Client.Models;
using Lykke.Tools.PrivateWalletBalanceReport.Repositories;

namespace Lykke.Tools.PrivateWalletBalanceReport.Services
{
    public interface IBalanceReader
    {
        Task<(string address, decimal amount)> ReadBalance(Asset asset, IPrivateWallet privateWallet);
        bool IsRelated(IPrivateWallet privateWallet);
    }
}
