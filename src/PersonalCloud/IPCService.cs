using System.Collections.Generic;

namespace NSPersonalCloud
{
    public interface IPCService
    {
        IReadOnlyList<PersonalCloud> PersonalClouds { get; }

        bool RemoveStorageProvider(string cloudId, string nodeName, bool saveChanges = true);
    }
}
