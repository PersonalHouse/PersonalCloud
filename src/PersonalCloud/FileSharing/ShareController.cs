using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;

using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;

using Newtonsoft.Json;

using NSPersonalCloud.FileSharing;
using NSPersonalCloud.Interfaces.Errors;
using NSPersonalCloud.Interfaces.FileSystem;

using Zio;

namespace NSPersonalCloud
{
#pragma warning disable CA1031

    public partial class ShareController : WebApiController
    {
        private readonly Zio.IFileSystem fileSystem;
        IPCService pCService;
        public ShareController(Zio.IFileSystem fs, IPCService pcService)
        {
            pCService = pcService;
            fileSystem = fs;
        }

        #region Utility

        public void SendJsonResponse(object response)
        {
            HttpContext.Response.ContentType = MimeType.Json;
            using var responseWriter = HttpContext.OpenResponseText(Encoding.UTF8, true, true);
            using var jsonWriter = new JsonTextWriter(responseWriter);
            JsonSerializer.CreateDefault().Serialize(jsonWriter, response);
        }

        #endregion Utility

        #region HTTP Methods

        [Route(HttpVerbs.Get, "/folder")]
        public async Task EnumerateAllChildren([QueryField("Path", true)] string path)
        {
            try
            {
                var children = fileSystem.EnumerateFileSystemEntries((new UPath(path)).ToAbsolute()).ToList();
                SendJsonResponse(children.Select(x => new Interfaces.FileSystem.FileSystemEntry(x)));
            }
            catch (NotSupportedException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotImplemented).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.BadRequest).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (SecurityException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (DirectoryNotFoundException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
            }
            catch (NotReadyException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.TooManyRequests).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _ = e.Message;
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
            }
        }

        [Route(HttpVerbs.Post, "/folder")]
        public async Task EnumerateChildren([QueryField("Path", true)] string path)
        {
            try
            {
                var data = await HttpContext.GetRequestDataAsync<EnumerateChildrenRequest>().ConfigureAwait(false);
                var children = fileSystem.EnumerateFileSystemEntries((new UPath(path)).ToAbsolute(), data.SearchPattern);
                SendJsonResponse(children.Select(x => new Interfaces.FileSystem.FileSystemEntry(x)).Skip(data.PageIndex).Take(data.PageSize));
            }
            catch (InvalidOperationException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.BadRequest).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (SecurityException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (DirectoryNotFoundException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
            }
            catch (NotReadyException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.TooManyRequests).ConfigureAwait(false);
            }
            catch
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
            }
        }

        [Route(HttpVerbs.Get, "/metadata")]
        public async Task ReadMetadata([QueryField("Path", true)] string path)
        {
            try
            {
                var fn = (new UPath(path)).ToAbsolute();
                var nodeInfo = fileSystem.GetFileSystemEntry(fn);
                SendJsonResponse(new Interfaces.FileSystem.FileSystemEntry(nodeInfo));
            }
            catch (InvalidOperationException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.BadRequest).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (SecurityException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (DirectoryNotFoundException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
            }
            catch (NotReadyException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.TooManyRequests).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _ = e.Message;
                Console.WriteLine($"exception in ReadMetadata {e.Message} {e.StackTrace}");
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
            }
        }

        [Route(HttpVerbs.Get, "/volume/freespace")]
        public async Task GetVolumeFreespace()
        {
            try
            {
                var info = new FreeSpaceInformation {
                    FreeBytesAvailable = 2L * 1024 * 1024 * 1024 * 1024,
                    TotalNumberOfBytes = 1L * 1024 * 1024 * 1024 * 1024,
                    TotalNumberOfFreeBytes = 2L * 1024 * 1024 * 1024 * 1024,
                };
                SendJsonResponse(info);
            }
            catch (InvalidOperationException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.BadRequest).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (SecurityException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (DirectoryNotFoundException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
            }
            catch (NotReadyException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.TooManyRequests).ConfigureAwait(false);
            }
            catch
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
            }
        }

        [Route(HttpVerbs.Put, "/folder")]
        public async Task CreateFolder([QueryField("Path", true)] string path)
        {
            try
            {
                fileSystem.CreateDirectory((new UPath(path)).ToAbsolute());
            }
            catch (InvalidOperationException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.BadRequest).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (SecurityException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
            }
        }

        private static async Task CopyStreamAsync(Stream input, Stream output, long bytes)
        {
            byte[] buffer = new byte[32768];
            int read;
            while (bytes > 0 &&
                   (read = await input.ReadAsync(buffer.AsMemory(0, (int) Math.Min((long) buffer.Length, bytes))).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                bytes -= read;
            }
        }

        [Route(HttpVerbs.Get, "/file")]
        public async Task ReadFile([QueryField("Path", true)] string path)
        {
            if (HttpContext.Request.Headers["Range"] is string range)
            {
                try
                {
                    range = range.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries)[1];
                    var rangeArray = range.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                    var from = long.Parse(rangeArray[0], NumberStyles.None, CultureInfo.InvariantCulture);
                    var to = long.Parse(rangeArray[1], NumberStyles.None, CultureInfo.InvariantCulture);

                    using var strmfile = fileSystem.OpenFile((new UPath(path)).ToAbsolute(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    strmfile.Seek(from, SeekOrigin.Begin);

                    var len = to - from + 1;
                    HttpContext.Response.Headers.Add(AuthDefinitions.HttpFileLength, (len + 8).ToString(CultureInfo.InvariantCulture));

#pragma warning disable CA1308 // Extension map is lowercase keyed.
                    HttpContext.Response.ContentType = MimeType.Associations.TryGetValue(Path.GetExtension(path)?.ToLowerInvariant(), out var mime) ? mime : MimeType.Default;
#pragma warning restore CA1308

                    using var target = HttpContext.OpenResponseStream(false, false);
                    using var strm = new HashWriteStream(target, len);

                    await CopyStreamAsync(strmfile, strm, len).ConfigureAwait(false);
                    //await source.CopyToAsync(strm).ConfigureAwait(false);
                }
                catch (InvalidOperationException e)
                {
                    _ = e.Message;
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.BadRequest).ConfigureAwait(false);
                }
                catch (UnauthorizedAccessException)
                {
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
                }
                catch (SecurityException)
                {
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
                }
                catch (FileNotFoundException)
                {
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
                }
                catch (NotReadyException)
                {
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.TooManyRequests).ConfigureAwait(false);
                }
                catch
                {
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
                }
            }
            else
            {
                try
                {
                    using var source = fileSystem.OpenFile((new UPath(path)).ToAbsolute(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    HttpContext.Response.Headers.Add(AuthDefinitions.HttpFileLength, (source.Length + 8).ToString(CultureInfo.InvariantCulture));

#pragma warning disable CA1308 // Extension map is lowercase keyed.
                    HttpContext.Response.ContentType = MimeType.Associations.TryGetValue(Path.GetExtension(path)?.ToLowerInvariant(), out var mime) ? mime : MimeType.Default;
#pragma warning restore CA1308

                    using var target = HttpContext.OpenResponseStream(false, false);
                    using var strm = new HashWriteStream(target, source.Length);

                    await source.CopyToAsync(strm).ConfigureAwait(false);
                }
                catch (InvalidOperationException e)
                {
                    _ = e.Message;
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.BadRequest).ConfigureAwait(false);
                }
                catch (UnauthorizedAccessException)
                {
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
                }
                catch (SecurityException)
                {
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
                }
                catch (FileNotFoundException)
                {
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
                }
                catch (NotReadyException)
                {
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.TooManyRequests).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _ = e.Message;
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
                }
            }
        }

        [Route(HttpVerbs.Put, "/file")]
        public async Task CreateFile([QueryField("Path", true)] string path)
        {
            try
            {
                using var origstream = HttpContext.OpenRequestStream();
                var len = long.Parse(HttpContext.Request.Headers[AuthDefinitions.HttpFileLength], CultureInfo.InvariantCulture);
                using var stream = new HashReadStream(origstream, false, len);

                var p = (new UPath(path)).ToAbsolute();
                var dir = p.GetDirectory();
                if (!fileSystem.DirectoryExists(dir))
                {
                    fileSystem.CreateDirectory(dir);
                }
                using var source = fileSystem.OpenFile(p, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
                await stream.CopyToAsync(source).ConfigureAwait(false);
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine($"CreateFile {e.Message} {e.StackTrace}");
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.BadRequest).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (SecurityException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _ = e.Message;
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
            }
        }

        [Route(HttpVerbs.Post, "/file")]
        public async Task WriteFile([QueryField("Path", true)] string path)
        {
            if (HttpContext.Request.Headers["Range"] is string range)
            {
                //bool isexpend = false;
                try
                {
                    range = range.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries)[1];
                    var rangeArray = range.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                    var from = long.Parse(rangeArray[0], NumberStyles.None, CultureInfo.InvariantCulture);
                    var to = long.Parse(rangeArray[1], NumberStyles.None, CultureInfo.InvariantCulture) - 8;//-crc64

                    using var origstream = HttpContext.OpenRequestStream();
                    var len = long.Parse(HttpContext.Request.Headers[AuthDefinitions.HttpFileLength], CultureInfo.InvariantCulture);

                    using var stream = new HashReadStream(origstream, false, len);

                    using var dest = fileSystem.OpenFile((new UPath(path)).ToAbsolute(), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                    //                     if (from==(dest.Length-1))
                    //                     {
                    //                         isexpend = true;
                    //                     }
                    dest.Seek(from, SeekOrigin.Begin);
                    await CopyStreamAsync(stream, dest, to - from + 1).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.BadRequest).ConfigureAwait(false);
                }
                catch (UnauthorizedAccessException)
                {
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
                }
                catch (SecurityException)
                {
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _ = e.Message;
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
                }
            }
            else
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
        }

        [Route(HttpVerbs.Post, "/file/length")]
        public async Task SetFileLength([QueryField("Path", true)] string path, [QueryField("Length", true)] long length)
        {
            try
            {
                using var dest = fileSystem.OpenFile((new UPath(path)).ToAbsolute(), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                dest.SetLength(length);
            }
            catch (InvalidOperationException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.BadRequest).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (SecurityException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
            }
            catch (DirectoryNotFoundException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
            }
            catch
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
            }
        }

        [Route(HttpVerbs.Post, "/file/attributes")]
        public async Task SetFileAttributes([QueryField("Path", true)] string path, [QueryField("Attributes", true)] int attributes)
        {
            try
            {
                fileSystem.SetAttributes((new UPath(path)).ToAbsolute(), (FileAttributes) attributes);
            }
            catch (InvalidOperationException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.BadRequest).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (SecurityException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
            }
            catch (DirectoryNotFoundException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
            }
            catch
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
            }
        }

        [Route(HttpVerbs.Post, "/file/timestamps")]
        public async Task SetFileTime([QueryField("Path", true)] string path,
                                      [QueryField("Created")] DateTime? creation,
                                      [QueryField("LastAccessed")] DateTime? access,
                                      [QueryField("LastWritten")] DateTime? write)
        {
            try
            {
                if (creation != null)
                {
                    fileSystem.SetCreationTime((new UPath(path)).ToAbsolute(), creation.Value);
                }
                if (access != null)
                {
                    fileSystem.SetLastAccessTime((new UPath(path)).ToAbsolute(), access.Value);
                }
                if (write != null)
                {
                    fileSystem.SetLastWriteTime((new UPath(path)).ToAbsolute(), write.Value);
                }
            }
            catch (InvalidOperationException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.BadRequest).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (SecurityException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
            }
            catch (DirectoryNotFoundException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
            }
            catch
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
            }
        }

        [Route(HttpVerbs.Post, "/move")]
        public async Task Move([QueryField("From", true)] string path, [QueryField("To", true)] string newName)
        {
            try
            {
                var fe = fileSystem.GetAttributes((new UPath(path)).ToAbsolute());
                if (fe.HasFlag(FileAttributes.Directory))
                {
                    fileSystem.MoveDirectory((new UPath(path)).ToAbsolute(), (new UPath(newName)).ToAbsolute() );
                }
                else
                {
                    fileSystem.MoveFile((new UPath(path)).ToAbsolute(), (new UPath(newName)).ToAbsolute() );
                }
            }
            catch (InvalidOperationException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.BadRequest).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (SecurityException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
            }
            catch (DirectoryNotFoundException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
            }
            catch
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
            }
        }

        [Route(HttpVerbs.Delete, "/file")]
        public async Task DeleteFile([QueryField("Path", true)] string path)
        {
            try
            {
                fileSystem.DeleteFile((new UPath(path)).ToAbsolute());
            }
            catch (InvalidOperationException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.BadRequest).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (SecurityException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
            }
            catch
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
            }
        }

        [Route(HttpVerbs.Delete, "/folder")]
        public async Task DeleteFolder([QueryField("Path", true)] string path)
        {
            try
            {
                if (fileSystem.DirectoryExists((new UPath(path)).ToAbsolute()))
                {
                    fileSystem.DeleteDirectory((new UPath(path)).ToAbsolute(), true);
                }
            }
            catch (InvalidOperationException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.BadRequest).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (SecurityException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.Forbidden).ConfigureAwait(false);
            }
            catch (DirectoryNotFoundException)
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
            }
            catch
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
            }
        }



        [Route(HttpVerbs.Get, "/cloud")]
        public async Task<PersonalCloudInfo> GetCloudInfo()
        {
            try
            {
                var pcid = HttpContext.Request.Headers[AuthDefinitions.AuthenticationPCId].Trim();
                if (string.IsNullOrWhiteSpace(pcid))
                {
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
                    return null;
                }

                var pc = pCService.PersonalClouds.First(x => x.Id == pcid);
                if (pc == null)
                {
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
                    return null;
                }
                return PersonalCloudInfo.FromPersonalCloud(pc);
            }
            catch
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
                return null;
            }
        }

        #endregion HTTP Methods
    }

#pragma warning restore CA1031
}
