using System;

namespace NSPersonalCloud.Interfaces.Errors
{
    public class InvalidParametersException : BaseException
    {
        public InvalidParametersException()
        {
        }

        public InvalidParametersException(string message) : base(message)
        {
        }

        public InvalidParametersException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
