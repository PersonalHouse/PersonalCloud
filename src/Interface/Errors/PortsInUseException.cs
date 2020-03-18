using System;

namespace NSPersonalCloud.Interfaces.Errors
{
    public class PortsInUseException : BaseException
    {
        public PortsInUseException() : base("All pre-defined network ports are in use by other applications.")
        {
        }

        public PortsInUseException(string message) : base(message)
        {
        }

        public PortsInUseException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
