using System;
using System.Collections.Generic;
using System.Text;

namespace NSPersonalCloud.Interfaces.FileSystem
{
    /// <summary>
    /// top level info, aka device info
    /// </summary>
    public class TopFileSystemEntry: FileSystemEntry
    {

        /// <summary>
        /// Node Id/Device Id
        /// </summary>
        public string NodeId { get; set; }
    }
}
