using System;
using System.Collections.Generic;
using System.Text;

namespace NSPersonalCloud
{
    public interface IPCService
    {
        public IReadOnlyList<PersonalCloud> PersonalClouds { get; }

        public void CleanExpiredNodes();
    }
}
