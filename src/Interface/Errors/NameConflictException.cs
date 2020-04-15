using System;

namespace NSPersonalCloud.Interfaces.Errors
{
    public class NameConflictException : BaseException
    {
        public NameConflictException()
        {
        }

        public NameConflictException(string message) : base(message)
        {
        }

        public NameConflictException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

}
