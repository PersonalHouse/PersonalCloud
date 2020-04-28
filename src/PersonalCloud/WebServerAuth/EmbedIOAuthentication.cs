using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Standart.Hash.xxHash;

namespace NSPersonalCloud.FileSharing
{
    public static class EmbedIOAuthentication
    {
        public static ulong? V1Auth(long ts, string strurl,byte[] key)
        {
            //when the node shows up, the node name is verified by aes key.
            //Therefor only hashing is used here to improve performance.

            try
            {
                if (strurl == null)
                {
                    return null;
                }

                using (var ms = new MemoryStream())
                {
                    using (var sw = new StreamWriter(ms))
                    {
                        sw.Write(ts);
                        sw.Write(strurl.ToUpperInvariant());
                        sw.Write(key);
                    }
                    var ba = ms.ToArray();

                    return xxHash64.ComputeHash(ba, ba.Length);
                }
            }finally
            {
            }
        }
    }
}
