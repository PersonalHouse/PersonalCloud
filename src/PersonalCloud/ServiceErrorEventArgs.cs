using System;

using NSPersonalCloud.Interfaces.Errors;

namespace NSPersonalCloud
{
    public class ServiceErrorEventArgs : EventArgs
    {
        public ErrorCode ErrorCode { get; }

        public ServiceErrorEventArgs(ErrorCode code)
        {
            ErrorCode = code;
        }
    }
}
