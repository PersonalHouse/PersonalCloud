using System;

namespace NSPersonalCloud.Interfaces.Errors
{
    public class InvalidDeviceResponseException : BaseException
    {
        public InvalidDeviceResponseException() : base("The active device returned invalid response.")
        {
        }

        public InvalidDeviceResponseException(string message) : base(message)
        {
        }

        public InvalidDeviceResponseException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
