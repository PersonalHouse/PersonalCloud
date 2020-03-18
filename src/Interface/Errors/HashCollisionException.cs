using System;

namespace NSPersonalCloud.Interfaces.Errors
{
    public class HashCollisionException : BaseException
    {
        public HashCollisionException()
        {
        }

        public HashCollisionException(string message) : base(message)
        {
        }

        public HashCollisionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
