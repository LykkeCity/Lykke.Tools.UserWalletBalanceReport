using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AzureStorage;
using AzureStorage.Tables;
using Lykke.AzureStorage.Tables;
using Lykke.Common.Log;
using Lykke.Service.BlockchainWallets.Contract;
using Lykke.SettingsReader;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Tools.UserWalletBalanceReport.Repositories
{
    public class BlockchainWalletsRepository
    {
        private readonly INoSQLTableStorage<BlockchainWalletEntity> _walletsTable;

        private BlockchainWalletsRepository(INoSQLTableStorage<BlockchainWalletEntity> walletsTable)
        {
            _walletsTable = walletsTable;
        }

        public static BlockchainWalletsRepository Create(IReloadingManager<string> connectionString, ILogFactory logFactory)
        {
            const string tableName = "BlockchainWallets";

            var walletsTable = AzureTableStorage<BlockchainWalletEntity>.Create
            (
                connectionString,
                tableName,
                logFactory
            );
            
            return new BlockchainWalletsRepository(walletsTable);
        }

        public async Task<(IEnumerable<BlockchainWalletEntity> Wallets, string ContinuationToken)> GetAllAsync(string blockchain, int take, string continuationToken)
        {
            IEnumerable<BlockchainWalletEntity> entities;

            var query = new TableQuery<BlockchainWalletEntity>();
            var filter = TableQuery.GenerateFilterCondition(nameof(BlockchainWalletEntity.IntegrationLayerId), QueryComparisons.Equal, blockchain);

            query.Where(filter);

            (entities, continuationToken) = await _walletsTable.GetDataWithContinuationTokenAsync(query, take, continuationToken);


            return (entities, continuationToken);
        }

        public class BlockchainWalletEntity : AzureTableEntity
        {
            public string Address { get; set; }

            public string IntegrationLayerId { get; set; }

            public Guid ClientId { get; set; }

            public CreatorType CreatedBy { get; set; }
        }
    }
}
