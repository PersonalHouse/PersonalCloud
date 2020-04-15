using System;

namespace NSPersonalCloud.Interfaces.Errors
{
    public class NoSuchCloudException : BaseException
    {
        public NoSuchCloudException()
        {
        }

        public NoSuchCloudException(string message) : base(message)
        {
        }

        public NoSuchCloudException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
