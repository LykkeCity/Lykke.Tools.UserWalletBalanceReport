using System;
using NBitcoin;
using NBitcoin.OpenAsset;

namespace Lykke.Tools.UserWalletBalanceReport.Services.Implementations.Bitcoin
{
    public static class MoneyExt
    {
        public static decimal GetAmount(this IMoney money, int multiplier)
        {
            switch (money)
            {
                case Money m:
                    return m.ToUnit(MoneyUnit.BTC);
                case AssetMoney am:
                    return am.ToDecimal(multiplier);
                default:
                    throw new InvalidCastException("Unknown type");

            }
        }
    }
}
