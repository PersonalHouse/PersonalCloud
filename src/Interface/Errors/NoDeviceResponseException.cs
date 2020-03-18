using System;

namespace NSPersonalCloud.Interfaces.Errors
{
    public class NoDeviceResponseException : BaseException
    {
        public NoDeviceResponseException() : base("No active device in this Cloud. Operation cannot proceed.")
        {
        }

        public NoDeviceResponseException(string message) : base(message)
        {
        }

        public NoDeviceResponseException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
