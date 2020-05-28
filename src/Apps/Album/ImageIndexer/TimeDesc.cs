using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NSPersonalCloud.Apps.Album.ImageIndexer
{
    internal class TimeDesc : IComparer<int>
    {
        public int Compare([AllowNull] int x, [AllowNull] int y)
        {
            return y - x;
        }
    }
}
