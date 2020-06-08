using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using EmbedIO;
using EmbedIO.WebApi;

using NSPersonalCloud;
using NSPersonalCloud.FileSharing;
using NSPersonalCloud.Interfaces.FileSystem;

using NUnit.Framework;
using NUnit.Framework.Interfaces;

using static System.Environment;

namespace LocalHosted
{
    internal sealed class HttpProvider : IDisposable
    {
        private WebServer Server { get; }
        private IFileSystem FileSystem { get; }

        public HttpProvider(int port, IFileSystem fileSystem)
        {
            FileSystem = fileSystem;
#pragma warning disable CA2000 // Dispose objects before losing scope
            Server = new WebServer(port).WithLocalSessionManager().WithWebApi("Share", "/api/share", module => module.WithController(CreateController));
#pragma warning restore CA2000 // Dispose objects before losing scope
        }

        public void Start() => Server?.Start();

        private ShareController CreateController() => new ShareController(FileSystem,null);

        public void Dispose()
        {
            Server?.Dispose();
        }
    }

    public class FileWebApiTest
    {
        private const string TestFileContent = "一行测试文字。\nこれはＴＥＳＴです。\nThis is a test.";

        private bool shouldContinue = true;
        private bool firstRun = true;

        public string TestRoot { get; private set; }

        private HttpProvider Server { get; set; }
        private TopFolderClient Client { get; set; }

        [OneTimeSetUp]
        public void Setup()
        {
            TestRoot = Path.Combine(GetFolderPath(SpecialFolder.MyDocuments), "Personal Cloud");
            Directory.CreateDirectory(TestRoot);
            TestRoot = Path.Combine(TestRoot, "Test Container");
            if (Directory.Exists(TestRoot))
            {
                Assert.Inconclusive("Previous test session may have failed. Please ensure \""
                                    + Path.GetDirectoryName(TestRoot)
                                    + "\" is empty before starting a new session.");
                return;
            }

            Directory.CreateDirectory(TestRoot);

            Server = new HttpProvider(10240, new VirtualFileSystem(TestRoot));
            Server.Start();

            Client = new TopFolderClient($"http://localhost:10240", new byte[32], "");
        }

        [OneTimeTearDown]
        public void Destroy()
        {
            try
            {
                Directory.Delete(TestRoot, true);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                // Ignored.
            }

            Client?.Dispose();
            Server?.Dispose();
        }

        [SetUp]
        public void RunNextTest()
        {
            if (firstRun && TestContext.CurrentContext.Test.MethodName != "SelfCheck")
            {
                Assert.Inconclusive("Due to the stateful nature of a filesystem, tests cannot be run out of order or in parallel."
                                    + NewLine
                                    + "Please execute all tests instead of running a particular one manually.");
                return;
            }
            if (!shouldContinue)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                Assert.Inconclusive("This test did not run because a previous test has failed.");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                return;
            }
        }

        [TearDown]
        public void PrepareNextTest()
        {
            firstRun = false;
            if (TestContext.CurrentContext.Result.Outcome.Status != TestStatus.Passed) shouldContinue = false;
        }

        [Test, Order(0)]
        public void SelfCheck()
        {
            // Reserved for the very first test.
            Assert.Pass();
        }

        [Test, Order(1)]
        public async Task CreateFile()
        {
            var source = Encoding.UTF8.GetBytes(TestFileContent);
            using var fileStream = new MemoryStream(source);
            await Client.WriteFileAsync("test.txt", fileStream).ConfigureAwait(false);

            using var fileStream2 = new MemoryStream(source);
            await Client.WriteFileAsync("test.txt", fileStream2).ConfigureAwait(false);

            using var empty = new MemoryStream(0);

            // Attempt to create file with illegal name.
            Assert.ThrowsAsync<HttpRequestException>(() => Client.WriteFileAsync(@"test\", fileStream).AsTask());
            // Attempt to create folder of the same name.
            Assert.ThrowsAsync<HttpRequestException>(() => Client.CreateDirectoryAsync("test.txt").AsTask());
        }

        [Test, Order(2)]
        public async Task CreateFolder()
        {
            await Client.CreateDirectoryAsync("Sample").ConfigureAwait(false);
            await Client.CreateDirectoryAsync(@"Sample\X\").ConfigureAwait(false);

            // Attempt to overwrite existing folder.
            Assert.ThrowsAsync<HttpRequestException>(() => Client.CreateDirectoryAsync("Sample").AsTask());
            using var empty = new MemoryStream(new byte[1]);
            // Attempt to create file of the same name.
            Assert.ThrowsAsync<HttpRequestException>(() => Client.WriteFileAsync("Sample", empty).AsTask());
        }

        [Test, Order(3)]
        public async Task Enumerate()
        {
            var items = await Client.EnumerateChildrenAsync(Path.AltDirectorySeparatorChar.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            Assert.IsNotNull(items);
            Assert.AreEqual(2, items.Count);

            items = await Client.EnumerateChildrenAsync(string.Empty).ConfigureAwait(false);
            Assert.IsNotNull(items);
            Assert.AreEqual(2, items.Count);

            items = await Client.EnumerateChildrenAsync(@"Sample\X\..").ConfigureAwait(false);
            Assert.IsNotNull(items);
            Assert.AreEqual(1, items.Count);

            // Relative path is supported as long as the absolute path falls within shared container.
            items = await Client.EnumerateChildrenAsync(@"Sample\X\..\..\..\Test Container").ConfigureAwait(false);
            Assert.IsNotNull(items);
            Assert.AreEqual(2, items.Count);

            items = await Client.EnumerateChildrenAsync(@"Sample\X\").ConfigureAwait(false);
            Assert.IsNotNull(items);
            Assert.IsEmpty(items);

            // Attempt to enumerate non-existent folder.
            Assert.ThrowsAsync<HttpRequestException>(() => Client.EnumerateChildrenAsync(@"Sample\X\不存在").AsTask());
        }

        [Test, Order(4)]
        public async Task ReadFile()
        {
            var source = Encoding.UTF8.GetBytes(TestFileContent);
            var target = new byte[source.Length];

            using var networkStream = await Client.ReadFileAsync("test.txt").ConfigureAwait(false);
            var bytesRead = await networkStream.ReadAsync(target).ConfigureAwait(false);
            Assert.AreEqual(target.Length, bytesRead);
            Assert.AreEqual(source, target);

            // Attempt to read non-existent file.
            Assert.ThrowsAsync<HttpRequestException>(() => Client.ReadFileAsync("What is this.txt").AsTask());
        }

        [Test, Order(5)]
        public async Task ReadPartialFile()
        {
            var source = Encoding.UTF8.GetBytes(TestFileContent);

            var partial1 = new byte[10];
            Buffer.BlockCopy(source, 3, partial1, 0, partial1.Length);
            var partial2 = new byte[20];
            Buffer.BlockCopy(source, 30, partial2, 0, partial2.Length);

            using var partialStream1 = await Client.ReadPartialFileAsync("test.txt", 3, 13).ConfigureAwait(false);
            var target = new byte[partial1.Length];
            var bytesRead = await partialStream1.ReadAsync(target).ConfigureAwait(false);
            Assert.AreEqual(target.Length, bytesRead);
            Assert.AreEqual(partial1, target);

            using var partialStream2 = await Client.ReadPartialFileAsync("test.txt", 30, 50).ConfigureAwait(false);
            target = new byte[partial2.Length];
            bytesRead = await partialStream2.ReadAsync(target).ConfigureAwait(false);
            Assert.AreEqual(target.Length, bytesRead);
            Assert.AreEqual(partial2, target);
        }

        [Test, Order(6)]
        public async Task Rename()
        {
            await Client.RenameAsync("test.txt", "Test.md").ConfigureAwait(false);
            await Client.RenameAsync("Sample/", "Some Folder").ConfigureAwait(false);
        }

        [Test, Order(7)]
        public async Task Delete()
        {
            await Client.DeleteAsync("Test.md").ConfigureAwait(false);
            await Client.DeleteAsync("Some Folder/").ConfigureAwait(false);

            Assert.ThrowsAsync<HttpRequestException>(() => Client.DeleteAsync("Unknown.md").AsTask());
            await Client.DeleteAsync("Unknown.md", true).ConfigureAwait(false);
            Assert.ThrowsAsync<HttpRequestException>(() => Client.DeleteAsync("Some Unknown Folder/", true).AsTask());
        }

        [Test, Order(8)]
        public async Task ObserveChanges()
        {
            Directory.CreateDirectory(Path.Combine(TestRoot, DateTime.Now.ToString("yyyyMMdd HHmmss", CultureInfo.InvariantCulture)));

            var items = await Client.EnumerateChildrenAsync(string.Empty).ConfigureAwait(false);
            Assert.IsNotNull(items);
            Assert.AreEqual(items.Count, 1);

            File.CreateText(Path.Combine(TestRoot, DateTime.Now.ToString("yyyyMMdd HHmmss", CultureInfo.InvariantCulture) + ".txt")).Dispose();

            items = await Client.EnumerateChildrenAsync(string.Empty).ConfigureAwait(false);
            Assert.IsNotNull(items);
            Assert.AreEqual(items.Count, 2);
        }

        [Test, Order(9)]
        public async Task PathTooLong()
        {
            await Client.CreateDirectoryAsync(Path.Combine("Wolahlegelsteinhausenbergerdorff",
                "黃宏成台灣阿成世界偉人財神總統",
                "Muvaffakiyetsizleştiricileştiriveremeyeileceklerimizdenmişsinizcesine",
                "กรุงเทพมหานคร อมรรัตนโกสินทร์ มหินทรายุทธยา นพรัตนราชธานีบุรีรมย์ อุดมราชนิเวศน์มหาสถาน อมรพิมานอวตารสถิต สักกะทัตติยะวิษณุกรรมประสิทธิ์"))
                .ConfigureAwait(false);

            using var empty = new MemoryStream(0);
            await Client.WriteFileAsync(Path.Combine("Wolfeschlegelsteinhausenbergerdorff", "Wolfeschlegelsteinhausenbergerdorff",
                "黃宏成台灣阿成世界偉人財神總統", "黃宏成台灣阿成世界偉人財神總統",
                "Muvaffakiyetsizleştiricileştiriveremeyebileceklerimizdenmişsinizcesine",
                "Muvaffakiyetsizleştiricileştiriveremeyebileceklerimizdenmişsinizcesine",
                "กรุงเทพมหานคร อมรรัตนโกสินทร์ มหินทรายุทธยา มหาดิลกภพ นพรัตนราชธานีบุรีรมย์ อุดมราชนิเวศน์มหาสถาน อมรพิมานอวตารสถิต สักกะทัตติยะวิษณุกรรมประสิทธิ์",
                "กรุงเทพมหานคร อมรรัตนโกสินทร์ มหินทรายุทธยา มหาดิลกภพ นพรัตนราชธานีบุรีรมย์ อุดมราชนิเวศน์มหาสถาน อมรพิมานอวตารสถิต สักกะทัตติยะวิษณุกรรมประสิทธิ์",
                "أفاستسقيناكموها", // Warning: Arabic is RTL! Do not attempt to edit this line without proper tools.
                "أفاستسقيناكموها", // RTL!
                "וְהָאֲחַשְׁדַּרְפְּנִים", // RTL!
                "וְהָאֲחַשְׁדַּרְפְּנִים", // RTL!
                "വെങ്കടനരസിംഹരാജുവാരിപേട്ട തീവണ്ടി നിലയം",
                "വെങ്കടനരസിംഹരാജുവാരിപേട്ട തീവണ്ടി നിലയം",
                "Wow.dat"), empty).ConfigureAwait(false);
        }

        [Test, Order(10)]
        public void AccessViolation()
        {
            Assert.ThrowsAsync<HttpRequestException>(() => Client.EnumerateChildrenAsync("../").AsTask());
            Assert.ThrowsAsync<HttpRequestException>(() => Client.CreateDirectoryAsync("../../I Am Groot").AsTask());
        }

        [Test, Order(11)]
        public void ConcurrentAccess()
        {
            // Todo: Can HttpClient send multiple requests at once? Multiple client needed?
            var clients = new List<Task>();

            foreach (var i in Enumerable.Range(1, 100))
            {
                clients.Add(Client.CreateDirectoryAsync($"Folder {i}").AsTask());
            }
            Task.WaitAll(clients.ToArray());
        }

        [Test, Order(12)]
        public async Task CreateFileZeroLength()
        {
            using var fileStream = new MemoryStream();
            await Client.WriteFileAsync("test.txt", fileStream).ConfigureAwait(false);

            var rs = await Client.ReadFileAsync("test.txt").ConfigureAwait(false);
            Assert.AreEqual(rs.Length, 0);
            var buf = new byte[1024];
            var ret = rs.Read(new Span<byte>(buf));
            Assert.AreEqual(ret, 0);
        }
    }
}
