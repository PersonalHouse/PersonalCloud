using System.Collections.Generic;
using System.IO;
using System.Linq;

using NSPersonalCloud.Interfaces.FileSystem;

namespace NSPersonalCloud.FileSharing
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<FileSystemEntry> SortDirectoryFirstByName(this IEnumerable<FileSystemEntry> files)
        {
            return files.Where(x => !x.Attributes.HasFlag(FileAttributes.System)
                                    && !x.Attributes.HasFlag(FileAttributes.Hidden))
                        .OrderByDescending(x => x.Attributes.HasFlag(FileAttributes.Directory))
                        .ThenBy(x => x.Name);
        }
    }
}
