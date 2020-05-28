using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;

using SQLite;

namespace NSPersonalCloud.Apps.Album
{
#pragma warning disable IDE0040 // Add accessibility modifiers
    public class AlbumWebController: WebApiController
#pragma warning restore IDE0040 // Add accessibility modifiers
    {
        Func<string, AlbumConfig> _authCb;
        public AlbumWebController(Func<string, AlbumConfig> authCb)
        {
            _authCb = authCb;
        }
        AlbumConfig AuthAndGetConfig()
        {
            var key = HttpContext?.Request?.Cookies["AccKey"];
            if (key==null)
            {
                return null;
            }
            return _authCb(key.Value);
        }
        [Route(HttpVerbs.Get, "/GetDays")]
        public async Task GetDays()
        {
            var cfg = AuthAndGetConfig();
            if (cfg==null)
            {
                HttpContext.Response.StatusCode = 403;
                return;
            }
            Console.WriteLine($"GetDays {cfg.ThumbnailFolder}");
            var s =  await File.ReadAllTextAsync(Path.Combine(cfg.ThumbnailFolder, Defines.YMDFileName)).ConfigureAwait(false);
            HttpContext.Response.ContentType = MimeType.Json;
            using var responseWriter = HttpContext.OpenResponseText(Encoding.UTF8);
            responseWriter.Write(s);
        }
        [Route(HttpVerbs.Get, "/GetMediaInDay")]
        public Task<List<ImageInfo>> GetMediaInDay([QueryField("y", true)]int year, [QueryField("m", true)]int month, [QueryField("d", true)]int day)
        {
            var cfg = AuthAndGetConfig();
            if (cfg == null)
            {
                HttpContext.Response.StatusCode = 403;
                return null;
            }
            using var dbConnection = new SQLiteConnection(Path.Combine(cfg.ThumbnailFolder, Defines.DBFileName), SQLiteOpenFlags.ReadWrite);
            var lis =  dbConnection.Query<ImageInfo>("select * from ImageInfo where year= ? and month= ? and day = ? order by MediaTime",
                year, month, day);
            return Task.FromResult(lis);
        }

        [Route(HttpVerbs.Get, "/GetMediaThumbnail")]
        public async Task GetMediaThumbnail([QueryField("id", true)]long id)
        {
            var cfg = AuthAndGetConfig();
            if (cfg == null)
            {
                HttpContext.Response.StatusCode = 403;
                return;
            }
            var fp = Path.Combine(cfg.ThumbnailFolder, Defines.ThumbnailFileName, $"{id}.jpg");
            using var rs = new FileStream(fp, FileMode.Open, FileAccess.Read);
            using var stream = HttpContext.OpenResponseStream();
            HttpContext.Response.ContentLength64 = rs.Length;
            await rs.CopyToAsync(stream).ConfigureAwait(false);
        }
        [Route(HttpVerbs.Get, "/GetMedia")]
        public async Task GetMedia([QueryField("id", true)]long id)
        {
            await GetFile(id,false).ConfigureAwait(false);
        }
        async Task GetFile(long id, bool asfile)
        {
            var cfg = AuthAndGetConfig();
            if (cfg == null)
            {
                HttpContext.Response.StatusCode = 403;
                return;
            }
            using var dbConnection = new SQLiteConnection(Path.Combine(cfg.ThumbnailFolder, Defines.DBFileName), SQLiteOpenFlags.ReadOnly);
            var p = dbConnection.Table<ImageInfo>().FirstOrDefault(x => x.Id == id);
            if (p != null)
            {
                var fp = Path.Combine(cfg.MediaFolder, p.Path);
                using var rs = new FileStream(fp, FileMode.Open, FileAccess.Read);

                if (HttpContext.Request.IsRangeRequest(rs.Length, p.MediaTime.ToString(CultureInfo.InvariantCulture),
                    DateTime.FromFileTimeUtc(p.MediaTime), out var start, out var upperBound))
                {
                    rs.Seek(start, SeekOrigin.Begin);
                    HttpContext.Response.StatusCode = 206;
                    HttpContext.Response.Headers["Content-Range"] = $"bytes {start}-{upperBound}/{rs.Length}";
                    //HttpContext.Response.ContentLength64 = upperBound- start+1;
                }
                else
                {
                    HttpContext.Response.ContentLength64 = rs.Length;
                }
                if (asfile)
                {
                    HttpContext.Response.Headers["Content-Disposition"] = $"attachment; filename = \"{Path.GetFileName(p.Path)}\"";
                }
                //HttpContext.Response.ContentType = "video/mp4";
                HttpContext.Response.ContentType = MimeType.Default;
                HttpContext.Response.Headers["Accept-Ranges"] = "bytes";
                using var stream = HttpContext.OpenResponseStream();
                await rs.CopyToAsync(stream).ConfigureAwait(false);
            }
        }
        [Route(HttpVerbs.Get, "/DownloadMedia")]
        public async Task DownloadMedia([QueryField("id", true)]long id)
        {
            await GetFile(id, true).ConfigureAwait(false);
        }
        //         [Route(HttpVerbs.Get, "/list")]
        //         public async Task<List<ImageInfo>> List(int start, int count)
        //         {
        // 
        //             using var dbConnection = new SQLiteConnection(Path.Combine(_albumFolder, Defines.DBFileName), SQLiteOpenFlags.ReadOnly);
        //             return dbConnection.Table<ImageInfo>().OrderByDescending(x => x.ExifDateTime)
        //             .Skip(start).Take(count).ToList();
        //         }

    }
}
