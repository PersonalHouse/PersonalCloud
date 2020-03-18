using System;

namespace NSPersonalCloud.Interfaces.Errors
{
    public class DeviceNotFoundException : BaseException
    {
        public DeviceNotFoundException() : base("An error occurred communicating with target device.")
        {
        }

        public DeviceNotFoundException(string message) : base(message)
        {
        }

        public DeviceNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
