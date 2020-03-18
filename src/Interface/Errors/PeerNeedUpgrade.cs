using System;
using System.Collections.Generic;
using System.Text;

namespace NSPersonalCloud.Interfaces.Errors
{
    public class PeerNeedUpgradeException : BaseException
    {
        public PeerNeedUpgradeException(string message) : base(message)
        {
        }

        public PeerNeedUpgradeException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public PeerNeedUpgradeException()
        {
        }
    }
}
