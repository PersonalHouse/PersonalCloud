using System;

namespace NSPersonalCloud.Interfaces.Errors
{
    public class DeviceInternalErrorException : BaseException
    {
        public DeviceInternalErrorException() : base("An internal error occurred on an active device. See inner exception.")
        {
        }

        public DeviceInternalErrorException(string message) : base(message)
        {
        }

        public DeviceInternalErrorException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
