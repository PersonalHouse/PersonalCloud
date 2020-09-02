using System.Collections.Generic;

namespace NSPersonalCloud
{
    public static class PathConsts
    {
        public static IReadOnlyList<char> InvalidCharacters { get; } = new char[] { '\u0000', '\u0001', '\u0002', '\u0003', '\u0004', '\u0005', '\u0006', '\u0007', '\b', '\t', '\n', '\u000B', '\f', '\r', '\u000E', '\u000F', '\u0010', '\u0011', '\u0012', '\u0013', '\u0014', '\u0015', '\u0016', '\u0017', '\u0018', '\u0019', '\u001A', '\u001B', '\u001C', '\u001D', '\u001E', '\u001F', '\u0022', '*', '/', ':', '\u003C', '\u003E', '?', '\\', '|' };
    }
}
