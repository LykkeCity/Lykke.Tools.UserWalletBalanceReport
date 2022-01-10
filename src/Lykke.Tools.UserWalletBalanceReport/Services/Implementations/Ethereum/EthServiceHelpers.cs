using System;
using System.Numerics;

namespace Lykke.Tools.UserWalletBalanceReport.Services.Implementations.Ethereum
{
    public static class EthServiceHelpers
    {
        public static string ConvertToContract(decimal amount, int multiplier, int accuracy)
        {
            if (accuracy > multiplier)
                throw new ArgumentException("accuracy > multiplier");

            amount *= (decimal)Math.Pow(10, accuracy);
            multiplier -= accuracy;
            var res = (BigInteger)amount * BigInteger.Pow(10, multiplier);

            return res.ToString();
        }

        public static decimal ConvertFromContract(string amount, int multiplier, int accuracy)
        {
            if (accuracy > multiplier)
                throw new ArgumentException("accuracy > multiplier");

            multiplier -= accuracy;

            var val = BigInteger.Parse(amount);
            var res = (decimal)(val / BigInteger.Pow(10, multiplier));
            res /= (decimal)Math.Pow(10, accuracy);

            return res;
        }

        public static int CalculateSign(string address, string from, string to)
        {
            int sign = 1;//incoming transaction;
            if (from?.ToLower() == address.ToLower())
            {
                sign = -1;
            }
            else if (from?.ToLower() == to?.ToLower())
            {
                sign = 0;
            }

            return sign;
        }
    }
}
