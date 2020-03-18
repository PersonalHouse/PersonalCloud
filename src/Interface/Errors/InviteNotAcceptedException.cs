using System;

namespace NSPersonalCloud.Interfaces.Errors
{
    public class InviteNotAcceptedException : BaseException
    {
        public InviteNotAcceptedException() : base("This invititation was not accepted by any active device.")
        {
        }

        public InviteNotAcceptedException(string message) : base(message)
        {
        }

        public InviteNotAcceptedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
