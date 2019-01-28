using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AsyncFriendlyStackTrace;
using AzureStorage.Tables;
using Lykke.Common.Log;
using Lykke.Logs;
using Lykke.Logs.Loggers.LykkeConsole;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.BlockchainWallets.Client;
using Lykke.Service.BlockchainWallets.Contract.Models;
using Lykke.Service.ClientAccount.Client;
using Lykke.SettingsReader;
using Lykke.Tools.UserWalletBalanceReport.Repositories;
using Lykke.Tools.UserWalletBalanceReport.Services;
using Lykke.Tools.UserWalletBalanceReport.Settings;
using Microsoft.Extensions.CommandLineUtils;
using Polly;

namespace Lykke.Tools.UserWalletBalanceReport
{
    internal static class Program
    {
        private const string SettingsFilePath = "settingsFilePath";

        static void Main(string[] args)
        {
            var application = new CommandLineApplication
            {
                Description = "Tool to obtain balance of private wallets in blockchain"
            };

            var arguments = new Dictionary<string, CommandArgument>
            {
                { SettingsFilePath, application.Argument(SettingsFilePath, "Url of a tool settings.") }
            };

            application.HelpOption("-? | -h | --help");
            application.OnExecute(async () =>
            {
                try
                {
                    if (arguments.Any(x => string.IsNullOrEmpty(x.Value.Value)))
                    {
                        application.ShowHelp();
                    }
                    else
                    {
                        await Execute(arguments[SettingsFilePath].Value);
                    }

                    return 0;
                }
                catch (Exception e)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e);

                    return 1;
                }
            });

            application.Execute(args);
        }

        private static async Task Execute(string settingsFilePath)
        {
            if (!File.Exists(settingsFilePath))
            {
                Console.WriteLine($"{settingsFilePath} file not exist");

                return;
            }

            var settings = new FileSettingsReader<ToolSettings>(settingsFilePath);

            var logFactory = LogFactory.Create()
                .AddConsole();

            var assetServce = new AssetsService(new Uri(settings.CurrentValue.AssetServiceUrl));

            
            var retryPolicy = Policy.Handle<RetryNeededException>()
                .WaitAndRetryAsync(10, retryAttempt => TimeSpan.FromSeconds(retryAttempt), onRetry:
                    (ex, delay, context, tsk) =>
                    {
                        Console.WriteLine($"Retrying exception {ex.ToAsyncString()}");
                    });

            var clientAccountService = new Lazy<ClientAccountClient>(()=> 
                new ClientAccountClient(settings.CurrentValue.ClientAccountUrl 
                                        ?? throw new ArgumentNullException(settings.CurrentValue.ClientAccountUrl)));

            IEnumerable<Asset> assets;
            if (!string.IsNullOrEmpty(settings.CurrentValue.AssetId))
            {

                var asset = await assetServce.AssetGetAsync(settings.CurrentValue.AssetId);

                if (asset == null)
                {
                    Console.WriteLine($"Asset not found {settings.CurrentValue.AssetId}");

                    return;
                }

                assets = new []
                {
                    asset
                };
            }
            else
            {
                assets = await assetServce.AssetGetAllAsync();
            }

            var balanceReaders = (BalanceReaderFactory.GetBalanceReaders(assets, settings.CurrentValue)).ToArray();
            
            const string csvDeliminator = ";";

            var counter = 0;
            string continuationToken = null;
            var relatedAssetsDictionary = new ConcurrentDictionary<Type, IEnumerable<Asset>>();

            do
            {
                IEnumerable<string> clientIds;

                if (string.IsNullOrEmpty(settings.CurrentValue.ClientIdsFilePath))
                {
               
                    Console.WriteLine("Retrieving client ids batch");
                    var response = await clientAccountService.Value.GetIdsAsync(continuationToken);

                    continuationToken = response.ContinuationToken;
                    clientIds = response.Ids;
                }
                else
                {
                    clientIds = await File.ReadAllLinesAsync(settings.CurrentValue.ClientIdsFilePath);
                }

                if (string.IsNullOrEmpty(settings.CurrentValue.AssetId) && settings.CurrentValue.IncludeZeroBalances)
                {
                    throw new ArgumentException("If AssetId is omitted, IncludeZeroBalances should be false.");
                }

                foreach (var clientId in clientIds)
                {
                    IEnumerable<(IBalanceReader balanceReader, string address)> addresses;

                    switch (settings.CurrentValue.WalletType)
                    {
                        case ToolSettings.WalletTypes.Private:
                            addresses = await GetPrivateWalletAddresses(clientId,
                                logFactory,
                                settings,
                                balanceReaders);

                            break;
                        case ToolSettings.WalletTypes.Deposit:
                            addresses = await GetDepositWallets(clientId,
                                logFactory,
                                settings,
                                balanceReaders);

                            break;
                        default:
                            throw new ArgumentException("Unknown switch", nameof(ToolSettings.WalletType));
                    }

                    foreach (var balanceReaderAddresses in addresses.GroupBy(p=>p.balanceReader.GetType()))
                    {
                        var balanceReader = balanceReaderAddresses.First().balanceReader;

                        //cache enumeration
                        var relatedAssets = relatedAssetsDictionary.GetOrAdd(balanceReader.GetType(),
                            (type) => balanceReader.SelectRelatedAssets(assets).ToList());

                        foreach (var address in balanceReader.SelectUniqueAddresses(balanceReaderAddresses.Select(p=>p.address)))
                        {
                            try
                            {
                                var blockchainBalances = await retryPolicy.ExecuteAsync(
                                    () => balanceReader.ReadBalance(relatedAssets, address));

                                foreach (var blockchainBalance in blockchainBalances
                                    .Where(p => p.amount != 0 || settings.CurrentValue.IncludeZeroBalances))
                                {
                                    await File.AppendAllTextAsync(settings.CurrentValue.ResultFilePath,
                                        string.Join(csvDeliminator,
                                            clientId,
                                            blockchainBalance.address,
                                            blockchainBalance.amount.ToString("F", CultureInfo.InvariantCulture),
                                            blockchainBalance.assetId)
                                        + Environment.NewLine);
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"Exception during processing balance for client {clientId}: {e.ToAsyncString()}");

                                await File.AppendAllTextAsync(settings.CurrentValue.ErrorFilePath,
                                    string.Join(csvDeliminator,
                                        DateTime.UtcNow,
                                        clientId,
                                        address,
                                        e.ToAsyncString())
                                    + Environment.NewLine);
                            }
                        }
                    }


                    counter++;
                    Console.WriteLine($"[{DateTime.UtcNow}] {clientId} done -- {counter}");
                }
            } while (continuationToken != null);

            Console.WriteLine("All done");
        }

        private static async Task<IEnumerable<(IBalanceReader balanceReader, string address)>> GetPrivateWalletAddresses(string clientId, 
            ILogFactory logFactory,
            IReloadingManager<ToolSettings> settings,
            IEnumerable<IBalanceReader> balanceReaders)
        {
            if (string.IsNullOrEmpty(settings.CurrentValue.Db.ClientPersonalInfoConnString))
            {
                throw new ArgumentNullException(nameof(ToolSettings.Db.ClientPersonalInfoConnString));
            }

            var privateWalletsRepo = new PrivateWalletsRepository(
                AzureTableStorage<PrivateWalletEntity>.Create(
                    settings.ConnectionString(x => x.Db.ClientPersonalInfoConnString),
                    "PrivateWallets", logFactory));

            var walletCredentialsRepo = new WalletCredentialsRepository(
                AzureTableStorage<WalletCredentialsEntity>.Create(
                    settings.ConnectionString(x => x.Db.ClientPersonalInfoConnString),
                    "WalletCredentials", logFactory));

            var wallets = (await privateWalletsRepo.GetAllPrivateWallets(clientId,
                await walletCredentialsRepo.GetAsync(clientId))).ToList();

            var result = new List<(IBalanceReader balanceReader, string address)>();
            foreach (var balanceReader in balanceReaders)
            {
                var addr = wallets.SelectMany(balanceReader.GetAddresses);
                result.AddRange(addr.Select(p => (balanceReader, p)));
            }

            return result;
        }

        private static async Task<IEnumerable<(IBalanceReader balanceReader, string address)>> GetDepositWallets(string clientId,
            ILogFactory logFactory,
            IReloadingManager<ToolSettings> settings,
            ICollection<IBalanceReader> balanceReaders)
        {
            if (string.IsNullOrEmpty(settings.CurrentValue.Db.ClientPersonalInfoConnString))
            {
                throw new ArgumentNullException(nameof(ToolSettings.Db.ClientPersonalInfoConnString));
            }

            if (string.IsNullOrEmpty(settings.CurrentValue.BlockchainWalletsUrl))
            {
                throw new ArgumentNullException(nameof(ToolSettings.Db.ClientPersonalInfoConnString));
            }

            var bcnCredentialsRepo = new BcnClientCredentialsRepository(
                AzureTableStorage<BcnCredentialsRecordEntity>.Create(
                    settings.ConnectionString(x => x.Db.ClientPersonalInfoConnString),
                    "BcnClientCredentials", logFactory));

            var walletCredentialsRepo = new WalletCredentialsRepository(
                AzureTableStorage<WalletCredentialsEntity>.Create(
                    settings.ConnectionString(x => x.Db.ClientPersonalInfoConnString),
                    "WalletCredentials", logFactory));

            var blockchainWalletsClient =
                new BlockchainWalletsClient(settings.CurrentValue.BlockchainWalletsUrl, logFactory);

            var result = new List<(IBalanceReader balanceReader, string address)>();

            var bcnCredWallets = (await bcnCredentialsRepo.GetAsync(clientId)).ToList();

            foreach (var balanceReader in balanceReaders)
            {
                var addr = bcnCredWallets.SelectMany(balanceReader.GetAddresses);
                result.AddRange(addr.Select(p=> (balanceReader, p)));
            }

            var walletCredentialsWallet = await walletCredentialsRepo.GetAsync(clientId);

            if (walletCredentialsWallet != null)
            {
                foreach (var balanceReader in balanceReaders)
                {
                    var addr = balanceReader.GetAddresses(walletCredentialsWallet);
                    result.AddRange(addr.Select(p => (balanceReader, p)));
                }
            }


            try
            {
                string continuationToken = null;
                do
                {
                    var resp = await 
                        blockchainWalletsClient.TryGetClientWalletsAsync(Guid.Parse(clientId), 10, continuationToken);

                    foreach (var balanceReader in balanceReaders)
                    {
                        var addr = (resp?.Wallets ?? Enumerable.Empty<BlockchainWalletResponse>())
                            .SelectMany(balanceReader.GetAddresses);
                        result.AddRange(addr.Select(p => (balanceReader, p)));
                    }

                    continuationToken = resp?.ContinuationToken;
                } while (continuationToken != null);
            }
            catch (ResultValidationException)
            {
                
            }


            return result;
        }
    }
}
