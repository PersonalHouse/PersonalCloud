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

namespace NSPersonalCloud
{
#pragma warning disable CA1031

    public partial class ShareController : WebApiController
    {
        private readonly IFileSystem fileSystem;
        IPCService pCService;
        public ShareController(IFileSystem vfs, IPCService pcService)
        {
            pCService = pcService;
            fileSystem = vfs;
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
                var children = await fileSystem.EnumerateChildrenAsync(path).ConfigureAwait(false);
                SendJsonResponse(children);
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
            catch
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
            }
        }

        [Route(HttpVerbs.Post, "/folder")]
        public async Task EnumerateChildren([QueryField("Path", true)] string path)
        {
            try
            {
                var data = await HttpContext.GetRequestDataAsync<EnumerateChildrenRequest>().ConfigureAwait(false);
                var children = await fileSystem.EnumerateChildrenAsync(path, data.SearchPattern, data.PageSize, data.PageIndex).ConfigureAwait(false);
                SendJsonResponse(children);
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
                var nodeInfo = await fileSystem.ReadMetadataAsync(path).ConfigureAwait(false);
                SendJsonResponse(nodeInfo);
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

        [Route(HttpVerbs.Get, "/volume/freespace")]
        public async Task GetVolumeFreespace()
        {
            try
            {
                var info = await fileSystem.GetFreeSpaceAsync().ConfigureAwait(false);
                if (info != null)
                {
                    SendJsonResponse(info);
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
                await fileSystem.CreateDirectoryAsync(path).ConfigureAwait(false);
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
                   (read = await input.ReadAsync(buffer, 0, (int) Math.Min((long) buffer.Length, bytes)).ConfigureAwait(false)) > 0)
            {
                output.Write(buffer, 0, read);
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

                    var len = to - from + 1;
                    using var source = await fileSystem.ReadPartialFileAsync(path, from, to).ConfigureAwait(false);
                    HttpContext.Response.Headers.Add(AuthDefinitions.HttpFileLength, (len + 8).ToString(CultureInfo.InvariantCulture));

#pragma warning disable CA1308 // Extension map is lowercase keyed.
                    HttpContext.Response.ContentType = MimeType.Associations.TryGetValue(Path.GetExtension(path)?.ToLowerInvariant(), out var mime) ? mime : MimeType.Default;
#pragma warning restore CA1308

                    using var target = HttpContext.OpenResponseStream(false, false);
                    using var strm = new WriteStream(target, len);

                    await CopyStreamAsync(source, strm, len).ConfigureAwait(false);
                    //await source.CopyToAsync(strm).ConfigureAwait(false);
                    strm.Dispose();
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
                    using var source = await fileSystem.ReadFileAsync(path).ConfigureAwait(false);
                    HttpContext.Response.Headers.Add(AuthDefinitions.HttpFileLength, (source.Length + 8).ToString(CultureInfo.InvariantCulture));

                    using var target = HttpContext.OpenResponseStream(false, false);
                    using var strm = new WriteStream(target, source.Length);

                    await source.CopyToAsync(strm).ConfigureAwait(false);
                    strm.Dispose();

#pragma warning disable CA1308 // Extension map is lowercase keyed.
                    HttpContext.Response.ContentType = MimeType.Associations.TryGetValue(Path.GetExtension(path)?.ToLowerInvariant(), out var mime) ? mime : MimeType.Default;
#pragma warning restore CA1308
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
                using var stream = new ReadStream(origstream, false, len);

                await fileSystem.WriteFileAsync(path, stream).ConfigureAwait(false);
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

        [Route(HttpVerbs.Post, "/file")]
        public async Task WriteFile([QueryField("Path", true)] string path)
        {
            if (HttpContext.Request.Headers["Range"] is string range)
            {
                try
                {
                    //                     if (!File.Exists(path))
                    //                     {
                    //                         await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
                    //                         return;
                    //                     }
                    range = range.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries)[1];
                    var rangeArray = range.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                    var from = long.Parse(rangeArray[0], NumberStyles.None, CultureInfo.InvariantCulture);
                    var to = long.Parse(rangeArray[1], NumberStyles.None, CultureInfo.InvariantCulture) - 8;//-crc64

                    using var origstream = HttpContext.OpenRequestStream();
                    var len = long.Parse(HttpContext.Request.Headers[AuthDefinitions.HttpFileLength], CultureInfo.InvariantCulture);

                    using var stream = new ReadStream(origstream, false, len);

                    await fileSystem.WritePartialFileAsync(path, from, to - from + 1, stream).ConfigureAwait(false);
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
                // TODO: For Test
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
            }
        }

        [Route(HttpVerbs.Post, "/file/length")]
        public async Task SetFileLength([QueryField("Path", true)] string path, [QueryField("Length", true)] long length)
        {
            try
            {
                await fileSystem.SetFileLengthAsync(path, length).ConfigureAwait(false);
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
                await fileSystem.SetFileAttributesAsync(path, (FileAttributes) attributes).ConfigureAwait(false);
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
                await fileSystem.SetFileTimeAsync(path, creation, access, write).ConfigureAwait(false);
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
                await fileSystem.RenameAsync(path, newName).ConfigureAwait(false);
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
        public async Task DeleteFile([QueryField("Path", true)] string path, [QueryField("Safe", false)] int safeDelete)
        {
            try
            {
                await fileSystem.DeleteAsync(path, safeDelete == 1).ConfigureAwait(false);
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
                await fileSystem.DeleteAsync(path).ConfigureAwait(false);
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
        public async Task GetCloudInfo()
        {
            try
            {
                var pcid = HttpContext.Request.Headers[AuthDefinitions.AuthenticationPCId].Trim();
                if (string.IsNullOrWhiteSpace(pcid))
                {
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
                    return;
                }

                var pc = pCService.PersonalClouds.First(x => x.Id == pcid);
                if (pc == null)
                {
                    await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.NotFound).ConfigureAwait(false);
                    return;
                }
                var pcinfo = PersonalCloudInfo.FromPersonalCloud(pc);
                SendJsonResponse(pcinfo);
            }
            catch
            {
                await HttpContext.SendStandardHtmlAsync((int) HttpStatusCode.InternalServerError).ConfigureAwait(false);
            }
        }

        #endregion HTTP Methods
    }

#pragma warning restore CA1031
}
