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
using Lykke.Service.ClientAccount.Client;
using Lykke.SettingsReader;
using Lykke.Tools.PrivateWalletBalanceReport.Repositories;
using Lykke.Tools.PrivateWalletBalanceReport.Services;
using Lykke.Tools.PrivateWalletBalanceReport.Settings;
using Microsoft.Extensions.CommandLineUtils;
using Polly;

namespace Lykke.Tools.PrivateWalletBalanceReport
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

            var privateWalletsRepo = new PrivateWalletsRepository(
                AzureTableStorage<PrivateWalletEntity>.Create(
                    settings.ConnectionString(x => x.Db.ClientPersonalInfoConnString),
                    "PrivateWallets", logFactory));

            var walletCredentialsRepo = new WalletCredentialsRepository(
                AzureTableStorage<WalletCredentialsEntity>.Create(
                    settings.ConnectionString(x => x.Db.ClientPersonalInfoConnString),
                    "WalletCredentials", logFactory));

            var retryPolicy = Policy.Handle<RetryNeededException>()
                .WaitAndRetryAsync(10, retryAttempt => TimeSpan.FromSeconds(retryAttempt), onRetry:
                    (ex, delay, context, tsk) =>
                    {
                        Console.WriteLine($"Retrying exception {ex.ToAsyncString()}");
                    });


            var asset = await assetServce.AssetGetAsync(settings.CurrentValue.AssetId);

            if (asset == null)
            {
                Console.WriteLine($"Asset not found {settings.CurrentValue.AssetId}");

                return;
            }


            var balanceReader = BalanceReaderFactory.GetBalanceReader(asset, settings.CurrentValue);
            string continuationToken = null;
            
            var csvDeliminator = ";";

            var counter = 0;
            do
            {

                IEnumerable<string> clientIds;

                if (string.IsNullOrEmpty(settings.CurrentValue.ClientIdsFilePath))
                {
                    var clientAccountService = new ClientAccountClient(settings.CurrentValue.ClientAccountUrl 
                                                                       ?? throw new ArgumentNullException(settings.CurrentValue.ClientAccountUrl));

                    Console.WriteLine("Retrieving client ids batch");
                    var response = await clientAccountService.GetIdsAsync(continuationToken);

                    continuationToken = response.ContinuationToken;
                    clientIds = response.Ids;
                }
                else
                {
                    clientIds = await File.ReadAllLinesAsync(settings.CurrentValue.ClientIdsFilePath);
                }


                foreach (var clientId in clientIds)
                {
                    var clientPrivateWallets = await privateWalletsRepo.GetAllPrivateWallets(clientId, await walletCredentialsRepo.GetAsync(clientId));

                    foreach (var wallet in clientPrivateWallets.Where(balanceReader.IsRelated))
                    {
                        try
                        {
                            var blockchainBalance = await retryPolicy.ExecuteAsync(
                                () => balanceReader.ReadBalance(asset, wallet));
                            
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
                                    wallet.WalletAddress,
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
    }
}
