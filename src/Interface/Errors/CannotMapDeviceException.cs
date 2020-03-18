using System;

namespace NSPersonalCloud.Interfaces.Errors
{
    public class CannotMapDeviceException : BaseException
    {
        public CannotMapDeviceException() : base("Cannot map an active device as top-level folder. See ineer exception.")
        {
        }

        public CannotMapDeviceException(string message) : base(message)
        {
        }

        public CannotMapDeviceException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
