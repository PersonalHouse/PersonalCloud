using System;

namespace NSPersonalCloud.Interfaces.Errors
{
    public class NotReadyException : Exception
    {
        public NotReadyException() { }

        public NotReadyException(string message) : base(message) { }

        public NotReadyException(string message, Exception innerException) : base(message, innerException) { }
    }
}
