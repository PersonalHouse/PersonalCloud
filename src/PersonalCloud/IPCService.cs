using System.Collections.Generic;

namespace NSPersonalCloud
{
    public interface IPCService
    {
        public IReadOnlyList<PersonalCloud> PersonalClouds { get; }

        public void CleanExpiredNodes();
    }
}
