using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using NSPersonalCloud.Interfaces.FileSystem;

namespace NSPersonalCloud.Interfaces.Cloud
{
    /// <summary>
    /// An overview of online devices in the Cloud, usually presented as folders in a disk/volume.
    /// </summary>
    public interface IDevicesView
    {
        ValueTask<List<FileSystemEntry>> EnumerateDevices(string path, CancellationToken cancellation = default);
    }
}
