using System.Collections.Generic;
using System.Threading.Tasks;
using AzureStorage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Tools.UserWalletBalanceReport.Repositories
{
    public class BcnCredentialsRecordEntity : TableEntity, IBcnCredentialsRecord
    {
        public static class ByClientId
        {
            public static string GeneratePartition(string clientId)
            {
                return clientId;
            }

            public static string GenerateRowKey(string assetId)
            {
                return assetId;
            }
        }


        public static class ByAssetAddress
        {
            public static string GeneratePartition()
            {
                return "ByAssetAddress";
            }

            public static string GenerateRowKey(string assetAddress)
            {
                return assetAddress;
            }
        }


        public string Address { get; set; }
        public string EncodedKey { get; set; }
        public string PublicKey { get; set; }
        public string ClientId { get; set; }
        public string AssetAddress { get; set; }
        public string AssetId { get; set; }
    }
    public class BcnClientCredentialsRepository : IBcnClientCredentialsRepository
    {
        private readonly INoSQLTableStorage<BcnCredentialsRecordEntity> _tableStorage;

        public BcnClientCredentialsRepository(INoSQLTableStorage<BcnCredentialsRecordEntity> tableStorage)
        {
            _tableStorage = tableStorage;
        }
        public async Task<IEnumerable<IBcnCredentialsRecord>> GetAsync(string clientId)
        {
            return await _tableStorage.GetDataAsync(BcnCredentialsRecordEntity.ByClientId.GeneratePartition(clientId));
        }
    }
}
