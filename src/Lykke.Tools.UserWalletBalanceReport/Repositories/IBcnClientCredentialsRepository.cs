using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lykke.Tools.UserWalletBalanceReport.Repositories
{
    public interface IBcnCredentialsRecord
    {
        string Address { get; set; }
        string EncodedKey { get; set; }
        string PublicKey { get; set; }
        string AssetId { get; set; }
        string ClientId { get; set; }
        string AssetAddress { get; set; }
    }

    public interface IBcnClientCredentialsRepository
    {
        Task<IEnumerable<IBcnCredentialsRecord>> GetAsync(string clientId);
    }
}
