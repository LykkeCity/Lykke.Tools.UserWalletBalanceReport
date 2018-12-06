using System;
using Lykke.Service.Assets.Client.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lykke.Tools.PrivateWalletBalanceReport.Repositories
{
    public interface IWalletCredentials
    {
        string ClientId { get; }
        string Address { get; }
        string PublicKey { get; }
        string PrivateKey { get; }
        string MultiSig { get; }
        string ColoredMultiSig { get; }
        bool PreventTxDetection { get; }
        string EncodedPrivateKey { get; }

        /// <summary>
        /// Conversion wallet is used for accepting BTC deposit and transfering needed LKK amount
        /// </summary>
        string BtcConvertionWalletPrivateKey { get; set; }
        string BtcConvertionWalletAddress { get; set; }

        /// <summary>
        /// Eth contract for user
        /// </summary>
        //ToDo: rename field to EthContract and change existing records
        string EthConversionWalletAddress { get; set; }
        string EthAddress { get; set; }
        string EthPublicKey { get; set; }

        string SolarCoinWalletAddress { get; set; }

        string ChronoBankContract { get; set; }

        string QuantaContract { get; set; }
    }


    public interface IWalletCredentialsRepository
    {
        Task<IWalletCredentials> GetAsync(string clientId);
    }

    public static class WalletCredentialsExt
    {


        public static string GetDepositAddressForAsset(this IWalletCredentials walletCredentials, Asset asset)
        {
            if (asset.Blockchain == Lykke.Service.Assets.Client.Models.Blockchain.Ethereum)
            {
                return null;
            }

            switch (asset.Id)
            {
                case "BTC":
                    return walletCredentials.MultiSig;
                case "SLR":
                    return walletCredentials.SolarCoinWalletAddress;
                case "TIME":
                    return walletCredentials.ChronoBankContract;
                case "QNT":
                    return walletCredentials.QuantaContract;
                default:
                    return walletCredentials.ColoredMultiSig;
            }
        }
    }
}
