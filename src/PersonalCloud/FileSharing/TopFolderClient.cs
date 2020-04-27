using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using Newtonsoft.Json;

using NSPersonalCloud.Interfaces.Errors;
using NSPersonalCloud.Interfaces.FileSystem;

namespace NSPersonalCloud.FileSharing
{
    public class TopFolderClient : IFileSystem, IDisposable
    {
        public const int RequestTimeoutInMs = 15 * 1000;//10s
        public string NodeId { get; set; }
        public string Name { get; set; }
        public long TimeStamp { get; set; }
        public Uri hostUri { get; set; }

        private byte[] Key;
        private string PcId;
        private readonly HttpClient httpClient;

        public TopFolderClient(string host, byte[] key, string pcid)
        {
            httpClient = new HttpClient();
            httpClient.Timeout = Timeout.InfiniteTimeSpan;
            hostUri = new Uri(host);
            Key = key;
            PcId = pcid;
        }

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                httpClient?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion IDisposable

        #region Utility

        private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri, params (string Key, string Value)[] queries)
        {
            var collection = HttpUtility.ParseQueryString(string.Empty);
            foreach (var pair in queries) collection.Add(pair.Key, pair.Value);

            var builder = new UriBuilder(hostUri) {
                Path = relativeUri,
                Query = collection.ToString()
            };

            var request = new HttpRequestMessage {
                Method = method,
                RequestUri = builder.Uri
            };

            return request;
        }

        protected async Task<HttpResponseMessage> SendRequest(HttpRequestMessage request, CancellationToken cancellation, bool AddTimeout = true)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var ts = DateTime.UtcNow.ToFileTime();
            request.Headers.Add(AuthDefinitions.AuthenticationVersion, AuthDefinitions.CurAuthVersion.ToString(CultureInfo.InvariantCulture));
            request.Headers.Add(AuthDefinitions.AuthenticationTimeStamp, ts.ToString(CultureInfo.InvariantCulture));
            request.Headers.Add(AuthDefinitions.AuthenticationPCId, PcId);
            var hash = EmbedIOAuthentication.V1Auth(ts, request.RequestUri.ToString().ToUpperInvariant(), Key);
            request.Headers.Add(AuthDefinitions.AuthenticationHash, hash.Value.ToString(CultureInfo.InvariantCulture));

            if (!AddTimeout)
            {
                if (cancellation != default)
                {
                    return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
                }
                else
                {
                    return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                }
            }
            else
            {
                using var ctstimeout = new CancellationTokenSource(RequestTimeoutInMs);
                if (cancellation != default)
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation, ctstimeout.Token);
                    return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                }
                else
                {
                    return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ctstimeout.Token).ConfigureAwait(false);
                }
            }
        }

        #endregion Utility

        /// <summary>
        /// [Route(HttpVerbs.Get, "/folder")]
        /// </summary>
        /// <param name="path">[QueryField("Path", true)]</param>
        public async ValueTask<List<FileSystemEntry>> EnumerateChildrenAsync(string path, CancellationToken cancellation = default)
        {
            using var request = CreateRequest(HttpMethod.Get, "/api/share/folder", ("Path", path));

            using var response = await SendRequest(request, cancellation).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<List<FileSystemEntry>>(json);
        }

        public async ValueTask<List<FileSystemEntry>> EnumerateChildrenAsync(string path, string searchPattern, int pageSize, int pageIndex, CancellationToken cancellation = default)
        {
            using var request = CreateRequest(HttpMethod.Post, "/api/share/folder", ("Path", path));

            var req = JsonConvert.SerializeObject(new { SearchPattern = searchPattern, PageSize = pageSize, PageIndex = pageIndex });
            using var stringContent = new StringContent(req, Encoding.UTF8, "application/json");
            request.Content = stringContent;

            using var response = await SendRequest(request, cancellation).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<List<FileSystemEntry>>(json);
        }

        public async ValueTask<FreeSpaceInformation> GetFreeSpaceAsync(CancellationToken cancellation = default)
        {
            using var request = CreateRequest(HttpMethod.Get, "/api/share/volume/freespace");

            using var response = await SendRequest(request, cancellation).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return (json != null) ? JsonConvert.DeserializeObject<FreeSpaceInformation>(json) : null;
        }

        /// <summary>
        /// [Route(HttpVerbs.Put, "/folder")]
        /// </summary>
        /// <param name="path">[QueryField("Path", true)]</param>
        public async ValueTask CreateDirectoryAsync(string path, CancellationToken cancellation = default)
        {
            using var request = CreateRequest(HttpMethod.Put, "/api/share/folder", ("Path", path));

            using var response = await SendRequest(request, cancellation).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// [Route(HttpVerbs.Get, "/metadata")]
        /// </summary>
        /// <param name="path">[QueryField("Path", true)]</param>
        public async ValueTask<FileSystemEntry> ReadMetadataAsync(string path, CancellationToken cancellation = default)
        {
            using var request = CreateRequest(HttpMethod.Get, "/api/share/metadata", ("Path", path));

            using var response = await SendRequest(request, cancellation).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<FileSystemEntry>(json);
        }

        /// <summary>
        /// [Route(HttpVerbs.Get, "/file")]
        /// </summary>
        /// <param name="path">[QueryField("Path", true)]</param>
        public async ValueTask<Stream> ReadFileAsync(string path, CancellationToken cancellation = default)
        {
            using var request = CreateRequest(HttpMethod.Get, "/api/share/file", ("Path", path));

            var response = await SendRequest(request, cancellation).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (!response.Headers.Contains(AuthDefinitions.HttpFileLength))
            {
                throw new PeerNeedUpgradeException();
            }
            var len = long.Parse(response.Headers.GetValues(AuthDefinitions.HttpFileLength).First(), CultureInfo.InvariantCulture);

            var strm = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return new ReadStream(strm, false, len);
        }

        /// <summary>
        /// [Route(HttpVerbs.Get, "/file")]
        /// </summary>
        /// <param name="path">[QueryField("Path", true)]</param>
        /// <param name="fromPosition">Request.Headers["Range"]</param>
        /// <param name="toPosition">Request.Headers["Range"]</param>
        public async ValueTask<Stream> ReadPartialFileAsync(string path, long fromPosition, long includeToPosition, CancellationToken cancellation = default)
        {
            using var request = CreateRequest(HttpMethod.Get, "/api/share/file", ("Path", path));
            request.Headers.Range = new RangeHeaderValue(fromPosition, includeToPosition);

            var response = await SendRequest(request, cancellation).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (!response.Headers.Contains(AuthDefinitions.HttpFileLength))
            {
                throw new PeerNeedUpgradeException();
            }
            var len = long.Parse(response.Headers.GetValues(AuthDefinitions.HttpFileLength).First(), CultureInfo.InvariantCulture);

            var strm = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return new ReadStream(strm, false, len);
        }

        /// <summary>
        /// [Route(HttpVerbs.Put, "/file")]
        /// </summary>
        /// <param name="path">[QueryField("Path", true)]</param>
        /// <param name="fileStream">[StreamContent]</param>
        public async ValueTask WriteFileAsync(string path, Stream fileStream, CancellationToken cancellation = default)
        {
            if (fileStream == null)
            {
                fileStream = Stream.Null;
            }
            using var request = CreateRequest(HttpMethod.Put, "/api/share/file", ("Path", path));
            request.Headers.Add(AuthDefinitions.HttpFileLength, (fileStream.Length + 8).ToString(CultureInfo.InvariantCulture));
            using var strm = new ReadStream(fileStream, true, 0, RequestTimeoutInMs, false);
            request.Content = new StreamContent(strm);

            if (cancellation == default)
            {
                using var response = await SendRequest(request, strm.TokenSource.Token, false).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
            else
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation, strm.TokenSource.Token);
                using var response = await SendRequest(request, cts.Token, false).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
        }

        public async ValueTask WritePartialFileAsync(string path, long position, long length, Stream fileStream, CancellationToken cancellation = default)
        {
            if (fileStream == null)
            {
                fileStream = Stream.Null;
            }
            using var request = CreateRequest(HttpMethod.Post, "/api/share/file", ("Path", path));
            request.Headers.Add(AuthDefinitions.HttpFileLength, (fileStream.Length + 8).ToString(CultureInfo.InvariantCulture));
            using var strm = new ReadStream(fileStream, true, 0, RequestTimeoutInMs, false);
            request.Content = new StreamContent(strm);
            request.Headers.Range = new RangeHeaderValue(position, position + length - 1 + 8);//+crc64

            if (cancellation == default)
            {
                using var response = await SendRequest(request, strm.TokenSource.Token, false).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
            else
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation, strm.TokenSource.Token);
                using var response = await SendRequest(request, cts.Token, false).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
        }

        /// <summary>
        /// [Route(HttpVerbs.Post, "/file/length")]
        /// </summary>
        /// <param name="path">[QueryField("Path", true)]</param>
        /// <param name="length">[QueryField("Length", true)]</param>
        public async ValueTask SetFileLengthAsync(string path, long length, CancellationToken cancellation = default)
        {
            using var request = CreateRequest(HttpMethod.Post, "/api/share/file/length", ("Path", path), ("Length", length.ToString(CultureInfo.InvariantCulture)));

            using var response = await SendRequest(request, cancellation).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// [Route(HttpVerbs.Post, "/file/attributes")]
        /// </summary>
        /// <param name="path">[QueryField("Path", true)]</param>
        /// <param name="attributes">[QueryField("Attributes", true)]</param>
        public async ValueTask SetFileAttributesAsync(string path, FileAttributes attributes, CancellationToken cancellation = default)
        {
            using var request = CreateRequest(HttpMethod.Post, "/api/share/file/attributes",
                ("Path", path), ("Attributes", ((int) attributes).ToString(CultureInfo.InvariantCulture)));

            using var response = await SendRequest(request, cancellation).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// [Route(HttpVerbs.Post, "/file/timestamps")]
        /// </summary>
        /// <param name="path">[QueryField("Path", true)]</param>
        /// <param name="creationTime">[QueryField("Created")]</param>
        /// <param name="lastAccessTime">[QueryField("LastAccessed")]</param>
        /// <param name="lastWriteTime">[QueryField("LastWritten")]</param>
        public async ValueTask SetFileTimeAsync(string path, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            var collection = HttpUtility.ParseQueryString(string.Empty);
            collection.Add("Path", path);
#pragma warning disable CA1305 // Round-trip format is culture invariant.
            if (creationTime.HasValue) collection.Add("Created", creationTime.Value.ToUniversalTime().ToString("o"));
            if (lastAccessTime.HasValue) collection.Add("LastAccessed", lastAccessTime.Value.ToUniversalTime().ToString("o"));
            if (lastWriteTime.HasValue) collection.Add("LastWritten", lastWriteTime.Value.ToUniversalTime().ToString("o"));
#pragma warning restore CA1305

            var builder = new UriBuilder(hostUri) {
                Path = "/api/share/file/timestamps",
                Query = collection.ToString()
            };

            using var request = new HttpRequestMessage {
                Method = HttpMethod.Post,
                RequestUri = builder.Uri
            };

            using var response = await SendRequest(request, cancellation).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// [Route(HttpVerbs.Post, "/move")]
        /// </summary>
        /// <param name="path">[QueryField("From", true)]</param>
        /// <param name="name">[QueryField("To", true)]</param>
        public async ValueTask RenameAsync(string path, string name, CancellationToken cancellation = default)
        {
            using var request = CreateRequest(HttpMethod.Post, "/api/share/move", ("From", path), ("To", name));

            using var response = await SendRequest(request, cancellation).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// [Route(HttpVerbs.Delete, "/folder")]
        /// [Route(HttpVerbs.Delete, "/file")]
        /// </summary>
        /// <param name="path">[QueryField("Path", true)]</param>
        public async ValueTask DeleteAsync(string path, bool safeDelete = false, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            var collection = HttpUtility.ParseQueryString(string.Empty);
            collection.Add("Path", path);
            if (safeDelete) collection.Add("Safe", "1");

            var builder = new UriBuilder(hostUri) {
                Query = collection.ToString()
            };

            using var request = new HttpRequestMessage {
                Method = HttpMethod.Delete
            };

            if (path.EndsWith('/'))
            {
                builder.Path = "/api/share/folder";
                request.RequestUri = builder.Uri;
            }
            else
            {
                builder.Path = "/api/share/file";
                request.RequestUri = builder.Uri;
            }

            using var response = await SendRequest(request, cancellation).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }


        public static async Task<string> GetCloudInfo(HttpClient lhttpClient, Uri url, string pcid, byte[] masterkey, CancellationToken cancellation = default)
        {
            if (lhttpClient==null)
            {
                throw new ArgumentNullException(nameof(lhttpClient));
            }
            using var request = new HttpRequestMessage {
                RequestUri = url
            };


            var ts = DateTime.UtcNow.ToFileTime();
            request.Headers.Add(AuthDefinitions.AuthenticationVersion, AuthDefinitions.CurAuthVersion.ToString(CultureInfo.InvariantCulture));
            request.Headers.Add(AuthDefinitions.AuthenticationTimeStamp, ts.ToString(CultureInfo.InvariantCulture));
            request.Headers.Add(AuthDefinitions.AuthenticationPCId, pcid);
            var hash = EmbedIOAuthentication.V1Auth(ts, request.RequestUri.ToString().ToUpperInvariant(), masterkey);
            request.Headers.Add(AuthDefinitions.AuthenticationHash, hash.Value.ToString(CultureInfo.InvariantCulture));

            HttpResponseMessage response = null;
            try
            {
                using var ctstimeout = new CancellationTokenSource(RequestTimeoutInMs);
                if (cancellation != default)
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation, ctstimeout.Token);
                    response = await lhttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                }
                else
                {
                    response = await lhttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ctstimeout.Token).ConfigureAwait(false);
                }

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            finally
            {
                response?.Dispose();
            }
        }
    }
}
