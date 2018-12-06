using System;

namespace Lykke.Tools.PrivateWalletBalanceReport.Services
{
    public class RetryNeededException:Exception
    {
        public RetryNeededException(Exception inner) : base(inner.Message, inner)
        {

        }
    }
}
