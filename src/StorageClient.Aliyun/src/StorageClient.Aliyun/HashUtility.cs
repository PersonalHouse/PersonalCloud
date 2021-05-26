using System;
using System.IO;
using System.Security.Cryptography;

namespace NSPersonalCloud.StorageClient.Aliyun
{
    internal static class HashUtility
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms", Justification = "<Pending>")]
        public static string CalulateHash_MD5(Stream stream, long skipBytes, long size)
        {
            string md5 = null;
            if (stream is FileStream)
            {
                using (MD5 md5Hash = MD5.Create())
                using (var md5stream = new PartialFileStream(((FileStream)stream).Name, FileMode.Open, FileAccess.Read, FileShare.Read, skipBytes, size))
                {
                    md5 = Convert.ToBase64String(md5Hash.ComputeHash(md5stream));
                }
            }
            else
            {
                using (MD5 md5Hash = MD5.Create())
                using (var md5stream = new PartialStream(stream, skipBytes, size))
                {
                    md5 = Convert.ToBase64String(md5Hash.ComputeHash(md5stream));
                }
            }
            return md5;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms", Justification = "<Pending>")]
        public static string CalulateHash_MD5(byte[] buffer, int offset, int size)
        {
            string md5 = null;
            using (MD5 md5Hash = MD5.Create())
            {
                md5 = Convert.ToBase64String(md5Hash.ComputeHash(buffer, offset, size));
            }
            return md5;
        }
    }
}
