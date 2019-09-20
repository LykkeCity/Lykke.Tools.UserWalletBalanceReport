using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AsyncFriendlyStackTrace;
using AzureStorage.Tables;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Logs;
using Lykke.Logs.Loggers.LykkeConsole;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;
using Lykke.Service.BlockchainWallets.Client;
using Lykke.Service.ClientAccount.Client;
using Lykke.SettingsReader;
using Lykke.Tools.UserWalletBalanceReport.Repositories;
using Lykke.Tools.UserWalletBalanceReport.Services;
using Lykke.Tools.UserWalletBalanceReport.Settings;
using Microsoft.Extensions.CommandLineUtils;
using NBitcoin;
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
            var log = logFactory.CreateLog("Program");

            var assetService = new AssetsService(new Uri(settings.CurrentValue.AssetServiceUrl));
            
            var asset = await assetService.AssetGetAsync(settings.CurrentValue.AssetId);

            if (asset == null)
            {
                Console.WriteLine($"Asset not found {settings.CurrentValue.AssetId}");

                return;
            }

            if (settings.CurrentValue.WalletType == ToolSettings.WalletTypes.BilDeposit)
            {
                await GetBilDepositWalletBalances(log, logFactory, settings, asset);
            }
            else
            {
                await GetBalancesByClients(settings, asset, logFactory, log);
            }

            Console.WriteLine("All done");
        }

        private static async Task GetBilDepositWalletBalances(ILog log, ILogFactory logFactory, FileSettingsReader<ToolSettings> settings, 
            Asset asset)
        {
            var balanceReader = BalanceReaderFactory.GetBalanceReader(asset, settings.CurrentValue);
            var repository = BlockchainWalletsRepository.Create(settings.Nested(x => x.Db).ConnectionString(x => x.BlockchainWalletsConnString), logFactory);

            string continuation = null;
            var counter = 0;

            do
            {
                var (wallets, continuationToken) = await repository.GetAllAsync(asset.BlockchainIntegrationLayerId, 1000, continuation);

                continuation = continuationToken;

                counter += await RenderBalances(log, settings, asset, balanceReader, wallets.Select(x => new UserWallet
                {
                    UserId = x.ClientId.ToString(),
                    Address = x.Address
                }));

                Console.WriteLine($"done -- {counter}");
            } while (continuation != null);
        }

        private static async Task GetBalancesByClients(FileSettingsReader<ToolSettings> settings, Asset asset, ILogFactory logFactory,
            ILog log)
        {
            var clientAccountService = new Lazy<ClientAccountClient>(() =>
                new ClientAccountClient(settings.CurrentValue.ClientAccountUrl
                                        ?? throw new ArgumentNullException(settings.CurrentValue.ClientAccountUrl)));

            var balanceReader = BalanceReaderFactory.GetBalanceReader(asset, settings.CurrentValue);
            string continuationToken = null;

            var counter = 0;
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


                foreach (var clientId in clientIds)
                {
                    IEnumerable<string> addresses;

                    switch (settings.CurrentValue.WalletType)
                    {
                        case ToolSettings.WalletTypes.Private:
                            addresses = await GetPrivateWalletAddresses(clientId,
                                logFactory,
                                settings,
                                balanceReader);

                            break;
                        case ToolSettings.WalletTypes.Deposit:
                            addresses = await GetDepositWallets(
                                log,
                                clientId,
                                asset,
                                logFactory,
                                settings,
                                balanceReader);
                            break;

                        default:
                            throw new ArgumentException("Unknown switch", nameof(ToolSettings.WalletType));
                    }

                    await RenderBalances(log, settings, asset, balanceReader, addresses.Select(address => new UserWallet
                    {
                        UserId = clientId,
                        Address = address
                    }));

                    counter++;
                    Console.WriteLine($"{clientId} done -- {counter}");
                }
            } while (continuationToken != null);
        }

        private static async Task<int> RenderBalances(ILog log, FileSettingsReader<ToolSettings> settings, Asset asset,
            IBalanceReader balanceReader, IEnumerable<UserWallet> wallets)
        {
            const string csvDeliminator = ";";

            var retryPolicy = Policy.Handle<RetryNeededException>()
                .WaitAndRetryAsync(10, retryAttempt => TimeSpan.FromSeconds(retryAttempt), onRetry:
                    (ex, delay, context, tsk) =>
                    {
                        Console.WriteLine($"Retrying exception {ex.ToAsyncString()}");
                    });

            int counter = 0;

            foreach (var wallet in wallets)
            {
                try
                {
                    var balance = await retryPolicy.ExecuteAsync(() => balanceReader.ReadBalance(asset, wallet.Address));

                    if (balance != 0 || settings.CurrentValue.IncludeZeroBalances)
                    {
                        var colored = BitcoinAddress.Create(wallet.Address, Network.Main).ToColoredAddress();

                        File.AppendAllText(settings.CurrentValue.ResultFilePath,
                            string.Join(csvDeliminator,
                                wallet.UserId,
                                wallet.Address,
                                colored,
                                balance.ToString("F", CultureInfo.InvariantCulture))
                            + Environment.NewLine);
                    }
                }
                catch (Exception e)
                {
                    log.Warning("Failed to render balance", exception: e, context: wallet);

                    File.AppendAllText(settings.CurrentValue.ErrorFilePath,
                        string.Join(csvDeliminator,
                            DateTime.UtcNow,
                            wallet.UserId,
                            wallet.Address,
                            e.ToAsyncString())
                        + Environment.NewLine);
                }

                ++counter;
            }

            return counter;
        }

        private static async Task<IEnumerable<string>> GetPrivateWalletAddresses(string clientId,
             ILogFactory logFactory,
             IReloadingManager<ToolSettings> settings,
             IBalanceReader balanceReader)
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

            return (await privateWalletsRepo.GetAllPrivateWallets(clientId,
                await walletCredentialsRepo.GetAsync(clientId)))
                .SelectMany(balanceReader.GetAddresses);
        }

        private static async Task<IEnumerable<string>> GetDepositWallets(ILog log,
            string clientId,
            Asset asset,
            ILogFactory logFactory,
            IReloadingManager<ToolSettings> settings,
            IBalanceReader balanceReader)
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

            var result = new List<string>();

            var bcnCredWallet = await bcnCredentialsRepo.GetAsync(clientId, asset.Id);

            if (bcnCredWallet != null)
            {
                result.AddRange(balanceReader.GetAddresses(bcnCredWallet));
            }

            var walletCredentialsWallet = await walletCredentialsRepo.GetAsync(clientId);

            if (walletCredentialsWallet != null)
            {
                result.AddRange(balanceReader.GetAddresses(walletCredentialsWallet));
            }

            var clientIdGuid = Guid.Parse(clientId);

            try
            {
                var bwWallet = await blockchainWalletsClient.GetAddressAsync(asset.BlockchainIntegrationLayerId,
                    asset.BlockchainIntegrationLayerAssetId, clientIdGuid);

                if (bwWallet != null)
                {
                    result.AddRange(balanceReader.GetAddresses(bwWallet));
                }
                else
                {
                    log.Warning("Deposit wallet not found", context: new
                    {
                        clientId,
                        clientIdGuid,
                        assetId = asset.BlockchainIntegrationLayerAssetId,
                        blockchainType = asset.BlockchainIntegrationLayerId
                    });
                }
            }
            catch (ResultValidationException e)
            {
                log.Warning("Error while getting the deposit wallet", exception: e, context: new
                {
                    clientId,
                    clientIdGuid,
                    assetId = asset.BlockchainIntegrationLayerAssetId,
                    blockchainType = asset.BlockchainIntegrationLayerId
                });
            }


            return result.Distinct();
        }
    }
}
