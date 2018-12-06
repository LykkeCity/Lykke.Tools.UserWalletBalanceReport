using System;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Lykke.SettingsReader;
using Lykke.SettingsReader.Helpers;
using Lykke.SettingsReader.ReloadingManager.Configuration;

namespace Lykke.Tools.PrivateWalletBalanceReport.Settings
{
    [PublicAPI]
    public class FileSettingsReader<TSettings> : ReloadingManagerWithConfigurationBase<TSettings>
    {
        private readonly string _settingsFilePath;

        public FileSettingsReader(string settingsFilePath)
        {
            if (string.IsNullOrEmpty(settingsFilePath))
                throw new ArgumentException("Path not specified.", nameof(settingsFilePath));

            _settingsFilePath = settingsFilePath;
        }

        protected override async Task<TSettings> Load()
        {
            Console.WriteLine($"{DateTime.UtcNow} Reading settings");
            
            var content = await File.ReadAllTextAsync(_settingsFilePath);
            var processingResult = await SettingsProcessor.ProcessForConfigurationAsync<TSettings>(content);
            var settings = processingResult.Item1;
            SetSettingsConfigurationRoot(processingResult.Item2);

            await SettingsProcessor.CheckDependenciesAsync(settings);

            return settings;
        }
    }
}
