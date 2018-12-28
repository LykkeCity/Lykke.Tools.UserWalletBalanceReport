using System;
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


            var asset = await assetServce.AssetGetAsync(settings.CurrentValue.AssetId);

            if (asset == null)
            {
                Console.WriteLine($"Asset not found {settings.CurrentValue.AssetId}");

                return;
            }


            var balanceReader = BalanceReaderFactory.GetBalanceReader(asset, settings.CurrentValue);
            string continuationToken = null;
            
            const string csvDeliminator = ";";

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
                            addresses = await GetDepositWallets(clientId, 
                                asset, logFactory, 
                                settings,
                                balanceReader);

                            break;
                        default:
                            throw new ArgumentException("Unknown switch", nameof(ToolSettings.WalletType));
                    }

                    foreach (var address in balanceReader.SelectUniqueAddresses(addresses))
                    {
                        try
                        {
                            var blockchainBalance = await retryPolicy.ExecuteAsync(
                                () => balanceReader.ReadBalance(asset, address));
                            
                            if (blockchainBalance.amount != 0 || settings.CurrentValue.IncludeZeroBalances)
                            {
                                File.AppendAllText(settings.CurrentValue.ResultFilePath,
                                    string.Join(csvDeliminator,
                                        clientId,
                                        blockchainBalance.address,
                                        blockchainBalance.amount.ToString("F", CultureInfo.InvariantCulture))
                                    + Environment.NewLine);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Exception during processing balance for client {clientId}: {e.ToAsyncString()}" );
            
                            File.AppendAllText(settings.CurrentValue.ErrorFilePath,
                                string.Join(csvDeliminator,
                                    DateTime.UtcNow,
                                    clientId,
                                    address,
                                    e.ToAsyncString())
                                + Environment.NewLine);
                        }
                    }

                    counter++;
                    Console.WriteLine($"{clientId} done -- {counter}");
                }
            } while (continuationToken != null);

            Console.WriteLine("All done");
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

            return  (await privateWalletsRepo.GetAllPrivateWallets(clientId,
                await walletCredentialsRepo.GetAsync(clientId)))
                .SelectMany(balanceReader.GetAddresses);
        }

        private static async Task<IEnumerable<string>> GetDepositWallets(string clientId, 
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

            try
            {
                var bwWallet = await blockchainWalletsClient.GetAddressAsync(asset.BlockchainIntegrationLayerId,
                    asset.BlockchainIntegrationLayerAssetId, Guid.Parse(clientId));

                if (bwWallet != null)
                {
                    result.AddRange(balanceReader.GetAddresses(bwWallet));
                }
            }
            catch (ResultValidationException e)
            {
                
            }


            return result.Distinct();
        }
    }
}
