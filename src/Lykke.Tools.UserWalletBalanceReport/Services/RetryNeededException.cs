using System;

namespace Lykke.Tools.UserWalletBalanceReport.Services
{
    public class RetryNeededException:Exception
    {
        public RetryNeededException(Exception inner) : base(inner.Message, inner)
        {

        }
    }
}
