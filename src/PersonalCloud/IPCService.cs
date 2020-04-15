using System.Collections.Generic;

namespace NSPersonalCloud
{
    public interface IPCService
    {
        IReadOnlyList<PersonalCloud> PersonalClouds { get; }

        void CleanExpiredNodes();

        bool RemoveStorageProvider(string cloudId, string nodeName, bool saveChanges = true);
    }
}
