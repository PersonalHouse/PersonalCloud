using System;
using SQLite;

namespace NSPersonalCloud.Apps.Album
{
    public class ImageInfo
    {
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; }
        [Indexed(Unique = true)]
        public string Path { get; set; }//relative path
        public long FileCreateTime { get; set; }//utc
        public long FileModifiedTime { get; set; }//utc
        public long FileSize { get; set; }
        public long LastCheckTime { get; set; }
        [Indexed(Name = "tm", Order = 1, Unique = false)]
        public short Year { get; set; }
        [Indexed(Name = "tm", Order = 2, Unique = false)]
        public byte Month { get; set; }
        [Indexed(Name = "tm", Order = 3, Unique = false)]
        public byte Day { get; set; }
        public byte IsVideo { get; set; }
        public byte IsWebImage { get; set; }

        public string Make { get; set; }
        public string Model { get; set; }
        public string Software { get; set; }
        [Indexed(Unique = false)]
        public long MediaTime { get; set; }
        public long? ExifDateTime { get; set; }
        public string StrExifDateTime { get; set; }
        //         public long DateTimeOriginal { get; set; }
        //         public long DateTimeDigitized { get; set; }
        public string LensMake { get; set; }
        public string LensModel { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Altitude { get; set; }

        public long Width { get; set; }
        public long Height { get; set; }

    }
}
