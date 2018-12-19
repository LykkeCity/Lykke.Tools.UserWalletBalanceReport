using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AzureStorage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Tools.UserWalletBalanceReport.Repositories
{
    public class PrivateWalletEntity : TableEntity, IPrivateWallet
    {
        public string ClientId { get; set; }
        public string WalletAddress { get; set; }
        public string WalletName { get; set; }
        public string EncodedPrivateKey { get; set; }
        public bool? IsColdStorage { get; set; }
        public int? Number { get; set; }
        public string BlockchainTypeFlat { get; set; }
        public Lykke.Service.Assets.Client.Models.Blockchain BlockchainType
        {
            get
            {
                Enum.TryParse(BlockchainTypeFlat, true, out Lykke.Service.Assets.Client.Models.Blockchain result);

                return result;
            }
            set
            {
                BlockchainTypeFlat = value.ToString();
            }
        }

        public static class ByClient
        {
            public static string GeneratePartitionKey(string clientId)
            {
                return clientId;
            }

            public static string GenerateRowKey(string address)
            {
                return address;
            }

            public static PrivateWalletEntity Create(IPrivateWallet privateWallet)
            {
                return new PrivateWalletEntity
                {
                    PartitionKey = GeneratePartitionKey(privateWallet.ClientId),
                    RowKey = GenerateRowKey(privateWallet.WalletAddress),
                    ClientId = privateWallet.ClientId,
                    WalletName = privateWallet.WalletName,
                    WalletAddress = privateWallet.WalletAddress,
                    EncodedPrivateKey = privateWallet.EncodedPrivateKey,
                    IsColdStorage = privateWallet.IsColdStorage,
                    BlockchainType = privateWallet.BlockchainType,
                    Number = privateWallet.Number,
                };
            }
        }

        public static class Record
        {
            public static string GeneratePartitionKey()
            {
                return "PrivateWallet";
            }

            public static string GenerateRowKey(string address)
            {
                return address;
            }

            public static PrivateWalletEntity Create(IPrivateWallet privateWallet)
            {
                return new PrivateWalletEntity
                {
                    PartitionKey = GeneratePartitionKey(),
                    RowKey = GenerateRowKey(privateWallet.WalletAddress),
                    ClientId = privateWallet.ClientId,
                    WalletName = privateWallet.WalletName,
                    WalletAddress = privateWallet.WalletAddress,
                    EncodedPrivateKey = privateWallet.EncodedPrivateKey,
                    IsColdStorage = privateWallet.IsColdStorage,
                    BlockchainType = privateWallet.BlockchainType,
                    Number = privateWallet.Number
                };
            }
        }
    }

    public class PrivateWalletsRepository : IPrivateWalletsRepository
    {
        private readonly INoSQLTableStorage<PrivateWalletEntity> _tableStorage;

        public PrivateWalletsRepository(INoSQLTableStorage<PrivateWalletEntity> tableStorage)
        {
            _tableStorage = tableStorage;
        }
        
        public async Task<IEnumerable<IPrivateWallet>> GetStoredWallets(string clientId)
        {
            var partition = PrivateWalletEntity.ByClient.GeneratePartitionKey(clientId);
            return await _tableStorage.GetDataAsync(partition);
        }
    }
}
