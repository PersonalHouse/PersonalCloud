
using FFmpeg.NET;
using ImageMagick;
using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace NSPersonalCloud.Apps.Album.ImageIndexer
{
    class ImgItemInfo
    {
        public string BaseFolder;
        public FileInfo fileInfo;
        public long TimeStamp;
    }
    class TimeDesc : IComparer<int>
    {
        public int Compare([AllowNull] int x, [AllowNull] int y)
        {
            return y - x;
        }
    }

    class ImgIndexer:IDisposable
    {
        private SQLiteConnection _dbConnection;
        ActionBlock<ImgItemInfo> imgqueue;
        const int ImageMaxSize = 512;
        string _dbFolder;
        SortedList<int, SortedList<int, SortedSet<int>>> YearMonthDays;

        //static string[] Extensions = { "bmp", "CR2", "CRW" };

        
        //lower case
        static string[] IgnoredExtensions = { ".mp3", ".wma", ".pdf", ".txt", ".dll", ".exe", ".ini", ".inf", 
            ".iso", ".zip", ".rar", ".7z", ".rtf", ".doc", ".docx", ".lrc" , ".html" , ".htm", ".csv", ".ds_store", 
            ".srt", ".db" , ".js" , ".css" };
        static string[] VideoExtensions = { ".mov", ".avi", ".mp4", ".flv", ".mkv", ".asf", ".wmv", ".rmvb",
            ".rm", ".swf",".mpg",".mpeg", ".vob"};
        public ImgIndexer(string dbfolder)
        {
            YearMonthDays = new SortedList<int, SortedList<int, SortedSet<int>>>(new TimeDesc());
            _dbFolder = dbfolder;
            _dbConnection = new SQLiteConnection(Path.Combine(_dbFolder, Defines.DBFileName), SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);
            _dbConnection.CreateTable<ImageInfo>();
            imgqueue = new ActionBlock<ImgItemInfo>(Process, 
               new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });
            //Environment.ProcessorCount });

        }

        public async Task Scan(string folder,long ts )
        {
            var dir = new DirectoryInfo(folder);
            if (!dir.Exists)
            {
                return;
            }
            var dirst = new List<DirectoryInfo>() { dir };
            while (dirst.Count>0)
            {
                var cur = dirst[0];
                dirst.RemoveAt(0);
                var dirs = cur.GetDirectories();
                dirst.AddRange(dirs);
                foreach (var item in cur.GetFiles())
                {
                    if (IsIgnoredFiles(item.Extension))
                    {
                        continue;
                    }
                    imgqueue.Post(new ImgItemInfo() { BaseFolder = folder, fileInfo = item, TimeStamp = ts });
                }
            }
            imgqueue.Complete();
            await imgqueue.Completion;
        }
        public void CleanNotExistImages(long ts)
        {
            var thpath = Path.Combine(_dbFolder, "Thumbnail");
            var lis = _dbConnection.Table<ImageInfo>().Where(x => x.LastCheckTime != ts).ToList();
            foreach (var item in lis)
            {
                File.Delete(Path.Combine(thpath, $"{item.Id}.jpg"));
                _dbConnection.Delete(item);
            }

        }

        
        async Task Process(ImgItemInfo info)
        {
            var file = info.fileInfo;
            try
            {
                var p = Path.GetRelativePath(info.BaseFolder, file.FullName);
                var isvideo = IsExtForVideo(file.Extension);
                if (isvideo==0)//whether known file format
                {
                    _ = Enum.Parse(typeof(MagickFormat), file.Extension.Substring(1), true);
                }

                if (!NeedUpdate(p, info.fileInfo, info.TimeStamp))
                {
                    return ;
                }

                var imginfo = _dbConnection.Table<ImageInfo>().FirstOrDefault(x => x.Path == p);
                if (imginfo == null)
                {
                    imginfo = new ImageInfo {
                        FileCreateTime = file.CreationTimeUtc.ToFileTime(),
                        FileSize = file.Length,
                        FileModifiedTime = file.LastWriteTimeUtc.ToFileTime(),
                        Path = p,
                    };
                    imginfo.MediaTime = imginfo.FileModifiedTime;
                    imginfo.IsVideo = isvideo;
                    _dbConnection.Insert(imginfo);
                }
                imginfo.LastCheckTime = info.TimeStamp;

                if (imginfo.IsVideo>0)
                {
                    UpdateYearMonthDay(imginfo);
                    await GenerateVideoThumbnail(file, imginfo);
                }
                else
                {
                    using (var image = new MagickImage(file.FullName))
                    {
                        var profile = image.GetExifProfile();
                        if (profile != null)
                        {
                            UpdateExif(imginfo, profile);
                            profile.RemoveThumbnail();
                        }

                        imginfo.Width = image.Width;
                        imginfo.Height = image.Height;

                        UpdateYearMonthDay(imginfo);

                        GenerateThumbnail(file, imginfo, image);
                        if (imginfo.Width==0)
                        {
                            var thpath = Path.Combine(_dbFolder, Defines.ThumbnailFileName);
                            Directory.CreateDirectory(thpath);
                            UpdateSizeFromThumbnail(imginfo, Path.Combine(thpath, $"{imginfo.Id}.jpg"));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unknown file extension {file.FullName}. {e.Message}");
            }
        }

        private async Task GenerateVideoThumbnail(FileInfo file, ImageInfo imginfo)
        {
            try
            {
                var thpath = Path.Combine(_dbFolder, Defines.ThumbnailFileName);
                Directory.CreateDirectory(thpath);

                var curdir = Path.GetDirectoryName(typeof(ImgIndexer).Assembly.Location);
                var des = Path.Combine(thpath, $"{imginfo.Id}.jpg");

                var outputFile = new MediaFile(des);
                var inputFile = new MediaFile(file.FullName);
                var ffmpeg = new Engine(Path.Combine(curdir, "ffmpeg.exe"));
                var metadata = await ffmpeg.GetMetaDataAsync(inputFile);
                if (metadata?.Duration.TotalSeconds > 5)
                {
                    var options = new ConversionOptions { Seek = TimeSpan.FromSeconds(5) };
                    await ffmpeg.GetThumbnailAsync(inputFile, outputFile, options);
                }
                else
                {
                    var options = new ConversionOptions { Seek = TimeSpan.FromSeconds(0) };
                    await ffmpeg.GetThumbnailAsync(inputFile, outputFile, options);
                }
                var fsizea = metadata?.VideoData?.FrameSize.Split('x', StringSplitOptions.RemoveEmptyEntries);
                if (fsizea?.Length == 2)
                {
                    imginfo.Width = int.Parse(fsizea[0]);
                    imginfo.Height = int.Parse(fsizea[1]);
                    _dbConnection.Update(imginfo);
                }
                else
                {
                    UpdateSizeFromThumbnail(imginfo, des);
                }
            }
            catch
            {
            }
        }

        private void UpdateSizeFromThumbnail(ImageInfo imginfo, string outputFile)
        {
            using (var image = new MagickImage(outputFile))
            {
                imginfo.Width = image.Width;
                imginfo.Height = image.Height;
                _dbConnection.Update(imginfo);
            }
        }

        private byte IsExtForVideo(string ext)
        {
            var ex = ext.ToLowerInvariant();
            if (VideoExtensions.FirstOrDefault(x => string.Compare(ex, x, false, CultureInfo.InvariantCulture) == 0) != null)
            {
                return 1;
            }
            return 0;
        }

        private bool IsIgnoredFiles(string ext)
        {
            var ex = ext.ToLowerInvariant();
            if (IgnoredExtensions.FirstOrDefault(x=> string.Compare(ex, x,false, CultureInfo.InvariantCulture) == 0)!=null)
            {
                return true;
            }
            return false;
        }

        private void UpdateYearMonthDay(ImageInfo imginfo)
        {
            DateTime tm = DateTime.FromFileTimeUtc(imginfo.MediaTime).ToLocalTime();
            imginfo.Year = (short)tm.Year;
            imginfo.Month = (byte)tm.Month;
            imginfo.Day = (byte)tm.Day;
            _dbConnection.Update(imginfo);

            lock (YearMonthDays)
            {
                if(!YearMonthDays.ContainsKey(imginfo.Year))
                {
                    YearMonthDays.Add(imginfo.Year, new SortedList<int, SortedSet<int>>(new TimeDesc()));
                }
                var mon = YearMonthDays[imginfo.Year];
                if (!mon.ContainsKey(imginfo.Month))
                {
                    mon.Add(imginfo.Month, new SortedSet<int>(new TimeDesc()));
                }
                var day = mon[imginfo.Month];
                if (!day.Contains(imginfo.Day))
                {
                    day.Add(imginfo.Day);
                }
            }
        }
        public async Task SaveYearMonthDays()
        {
            var p = Path.Combine(_dbFolder, Defines.YMDFileName);
            await File.WriteAllTextAsync(p, Newtonsoft.Json.JsonConvert.SerializeObject(YearMonthDays));
        }

        private void GenerateThumbnail(FileInfo file, ImageInfo imginfo, MagickImage image)
        {
            var thpath = Path.Combine(_dbFolder, Defines.ThumbnailFileName);
            Directory.CreateDirectory(thpath);

            var ext = file.Extension;
            if (CouldFileTypeShowInWeb(ext, image)|| (imginfo.IsVideo>0))
            {
                MagickGeometry size = null;
                var width = image.Width;
                var height = image.Height;
                if ((width < ImageMaxSize) && (height < ImageMaxSize))
                {
                    size = new MagickGeometry(width, height);
                }
                else
                {
                    size = new MagickGeometry(ImageMaxSize, ImageMaxSize);
                }
                size.IgnoreAspectRatio = false;
                image.Resize(size);
                image.Quality = 60;
                imginfo.IsWebImage = 1;
            }
            image.Write(Path.Combine(thpath, $"{imginfo.Id}.jpg"));
        }

        private bool CouldFileTypeShowInWeb(string ext, MagickImage image)
        {
            if (image.Format==MagickFormat.Jpeg)
            {
                return true;
            }
            if (image.Format == MagickFormat.Png)
            {
                return true;
            }
            return false;
        }

        private void UpdateExif(ImageInfo imginfo, IExifProfile profile)
        {
            bool isnorth = true;
            bool iseast = true;
            bool abovesea = true;
            foreach (IExifValue value in profile.Values)
            {
                if (string.Compare(value.Tag.ToString(), "Make", true, CultureInfo.InvariantCulture) == 0)
                {
                    imginfo.Make = (string)value.GetValue();
                }
                else if (string.Compare(value.Tag.ToString(), "Model", true, CultureInfo.InvariantCulture) == 0)
                {
                    imginfo.Model = (string)value.GetValue();
                }
                else if (string.Compare(value.Tag.ToString(), "Software", true, CultureInfo.InvariantCulture) == 0)
                {
                    imginfo.Software = (string)value.GetValue();
                }
                else if (string.Compare(value.Tag.ToString(), "LensMake", true, CultureInfo.InvariantCulture) == 0)
                {
                    imginfo.LensMake = (string)value.GetValue();
                }
                else if (string.Compare(value.Tag.ToString(), "LensModel", true, CultureInfo.InvariantCulture) == 0)
                {
                    imginfo.LensModel = (string)value.GetValue();
                }
                else if (string.Compare(value.Tag.ToString(), "DateTime", true, CultureInfo.InvariantCulture) == 0)
                {
                    imginfo.StrExifDateTime = (string)value.GetValue();
                    imginfo.ExifDateTime = ParseDatetime(imginfo.StrExifDateTime);
                }
                else if (string.Compare(value.Tag.ToString(), "DateTimeOriginal", true, CultureInfo.InvariantCulture) == 0)
                {
                    if (string.IsNullOrWhiteSpace(imginfo.StrExifDateTime))
                    {
                        imginfo.StrExifDateTime = (string)value.GetValue();
                        imginfo.ExifDateTime = ParseDatetime(imginfo.StrExifDateTime);
                    }
                }
                else if (string.Compare(value.Tag.ToString(), "DateTimeDigitized", true, CultureInfo.InvariantCulture) == 0)
                {
                    if (string.IsNullOrWhiteSpace(imginfo.StrExifDateTime))
                    {
                        imginfo.StrExifDateTime = (string)value.GetValue();
                        imginfo.ExifDateTime = ParseDatetime(imginfo.StrExifDateTime);
                    }
                }
                else if (string.Compare(value.Tag.ToString(), "GPSLatitudeRef", true, CultureInfo.InvariantCulture) == 0)
                {
                    var n = (string)value.GetValue();
                    if (string.Compare(n, "N", true, CultureInfo.InvariantCulture) == 0)
                    {
                        isnorth = true;
                    }
                    else
                    {
                        isnorth = false;
                    }
                }
                else if (string.Compare(value.Tag.ToString(), "GPSLatitude", true, CultureInfo.InvariantCulture) == 0)
                {
                    var rs = (Rational[])value.GetValue();
                    double v = 0;
                    int e = 1;
                    for (int i = 0; i < rs.Length; i++)
                    {
                        v += ((double)rs[i].Numerator) / (rs[i].Denominator * e);
                        e *= 60;
                    }
                    imginfo.Latitude = v;
                }
                else if (string.Compare(value.Tag.ToString(), "GPSLongitudeRef", true, CultureInfo.InvariantCulture) == 0)
                {
                    var e = (string)value.GetValue();
                    if (string.Compare(e, "E", true, CultureInfo.InvariantCulture) == 0)
                    {
                        iseast = true;
                    }
                    else
                    {
                        iseast = false;
                    }
                }
                else if (string.Compare(value.Tag.ToString(), "GPSLongitude", true, CultureInfo.InvariantCulture) == 0)
                {
                    var rs = (Rational[])value.GetValue();
                    double v = 0;
                    int e = 1;
                    for (int i = 0; i < rs.Length; i++)
                    {
                        v += ((double)rs[i].Numerator) / (rs[i].Denominator * e);
                        e *= 60;
                    }
                    imginfo.Longitude = v;
                }
                else if (string.Compare(value.Tag.ToString(), "GPSAltitudeRef", true, CultureInfo.InvariantCulture) == 0)
                {
                    var e = (byte)value.GetValue();
                    if (e == 0)
                    {
                        abovesea = true;
                    }
                    else
                    {
                        abovesea = false;
                    }
                }
                else if (string.Compare(value.Tag.ToString(), "GPSAltitude", true, CultureInfo.InvariantCulture) == 0)
                {
                    var rs = (Rational)value.GetValue();
                    imginfo.Altitude = ((double)rs.Numerator) / rs.Denominator ;
                }
            }
            if (imginfo.Latitude != null)
            {
                if (!isnorth)
                {
                    imginfo.Latitude = 0 - imginfo.Latitude;
                }
            }
            if (imginfo.Longitude != null)
            {
                if (!iseast)
                {
                    imginfo.Longitude = 0 - imginfo.Longitude;
                }
            }
            if (imginfo.Altitude != null)
            {
                if (!abovesea)
                {
                    imginfo.Altitude = 0 - imginfo.Altitude;
                }
            }
            if (imginfo.ExifDateTime != null)
            {
                imginfo.MediaTime = imginfo.ExifDateTime.Value;
            }
            _dbConnection.Update(imginfo);
        }

        private long? ParseDatetime(string strExifDateTime)
        {
            string format = "yyyy:MM:dd HH:mm:ss";
            try
            {
                return DateTime.ParseExact(strExifDateTime, format, CultureInfo.InvariantCulture).ToFileTime();
            }
            catch (Exception )
            {
            }
            try
            {
                return DateTime.Parse(strExifDateTime, CultureInfo.InvariantCulture).ToFileTime();
            }
            catch (Exception )
            {
            }
            return null;
        }

        private bool NeedUpdate(string p, FileInfo info,long ts)
        {
            var fiindb = _dbConnection.Table<ImageInfo>().FirstOrDefault(x => x.Path == p);
            if (fiindb == null)
            {
                return true;
            }
            if (fiindb.FileModifiedTime != info.LastWriteTimeUtc.ToFileTime())
            {
                return true;
            }
            if (fiindb.FileSize != info.Length)
            {
                return true;
            }
            fiindb.LastCheckTime = ts;
            _dbConnection.Execute($"update ImageInfo set LastCheckTime={ts} where Id={fiindb.Id}");
            return false;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _dbConnection?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ImgIndexer()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
