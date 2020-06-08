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

using Microsoft.Extensions.Logging;

using NSPersonalCloud;
using NSPersonalCloud.FileSharing;
using NSPersonalCloud.Interfaces.FileSystem;

using NUnit.Framework;
using NUnit.Framework.Interfaces;

using static System.Environment;

namespace LocalHosted
{


    public class FileContainerWebApiTest
    {
        private const string TestFileContent = "一行测试文字。\nこれはＴＥＳＴです。\nThis is a test.";

        private bool shouldContinue = true;
        private bool firstRun = true;

        public string TestRoot { get; private set; }

        private HttpProvider Server { get; set; }
        private TopFolderClient Client { get; set; }
        ILoggerFactory Loggers;

        [OneTimeSetUp]
        public void Setup()
        {
            var logdir  = TestRoot = Path.Combine(GetFolderPath(SpecialFolder.MyDocuments), "Personal Cloud");
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

            Directory.CreateDirectory(logdir);

            Loggers = LoggerFactory.Create(builder => builder.//SetMinimumLevel(LogLevel.Trace).
            AddConsole(x => {
                x.TimestampFormat = "G";
            }));


            var dic = new Dictionary<string, IFileSystem>();
            dic["Files"] = new VirtualFileSystem(TestRoot);

            var RootFs = new FileSystemContainer(dic, Loggers.CreateLogger("FileContainerWebApiTest"));

            Server = new HttpProvider(10240, RootFs);
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
            Loggers?.Dispose();
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
            await Client.WriteFileAsync("Files/test.txt", fileStream).ConfigureAwait(false);

            using var empty = new MemoryStream(0);
            // Attempt to overwrite existing file.
            Assert.ThrowsAsync<HttpRequestException>(() => Client.WriteFileAsync("test.txt", fileStream).AsTask());
            // Attempt to create file with illegal name.
            Assert.ThrowsAsync<HttpRequestException>(() => Client.WriteFileAsync(@"test\", fileStream).AsTask());
            // Attempt to create folder of the same name.
            Assert.ThrowsAsync<HttpRequestException>(() => Client.CreateDirectoryAsync("Files/test.txt").AsTask());
        }

        [Test, Order(2)]
        public async Task CreateFolder()
        {
            await Client.CreateDirectoryAsync("Files/Sample").ConfigureAwait(false);
            await Client.CreateDirectoryAsync(@"Files/Sample\X\").ConfigureAwait(false);

            // Attempt to overwrite existing folder.
            Assert.ThrowsAsync<HttpRequestException>(() => Client.CreateDirectoryAsync("Files/Sample").AsTask());
            using var empty = new MemoryStream(0);
            // Attempt to create file of the same name.
            Assert.ThrowsAsync<HttpRequestException>(() => Client.WriteFileAsync("Files/Sample", empty).AsTask());
        }

        [Test, Order(3)]
        public async Task Enumerate()
        {
            var items = await Client.EnumerateChildrenAsync("Files/").ConfigureAwait(false);
            Assert.IsNotNull(items);
            Assert.AreEqual(2, items.Count);

            items = await Client.EnumerateChildrenAsync(string.Empty).ConfigureAwait(false);
            Assert.IsNotNull(items);
            Assert.AreEqual(1, items.Count);

            items = await Client.EnumerateChildrenAsync(@"Files/Sample\X\..").ConfigureAwait(false);
            Assert.IsNotNull(items);
            Assert.AreEqual(1, items.Count);

            // Relative path is supported as long as the absolute path falls within shared container.
            items = await Client.EnumerateChildrenAsync(@"Files/Sample\X\..\..\..\Test Container").ConfigureAwait(false);
            Assert.IsNotNull(items);
            Assert.AreEqual(2, items.Count);

            items = await Client.EnumerateChildrenAsync(@"Files/Sample\X\").ConfigureAwait(false);
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

            using var networkStream = await Client.ReadFileAsync("Files/test.txt").ConfigureAwait(false);
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

            using var partialStream1 = await Client.ReadPartialFileAsync("Files/test.txt", 3, 13).ConfigureAwait(false);
            var target = new byte[partial1.Length];
            var bytesRead = await partialStream1.ReadAsync(target).ConfigureAwait(false);
            Assert.AreEqual(target.Length, bytesRead);
            Assert.AreEqual(partial1, target);

            using var partialStream2 = await Client.ReadPartialFileAsync("Files/test.txt", 30, 50).ConfigureAwait(false);
            target = new byte[partial2.Length];
            bytesRead = await partialStream2.ReadAsync(target).ConfigureAwait(false);
            Assert.AreEqual(target.Length, bytesRead);
            Assert.AreEqual(partial2, target);
        }

        [Test, Order(6)]
        public async Task Rename()
        {
            await Client.RenameAsync("Files/test.txt", "Files/Test.md").ConfigureAwait(false);
            await Client.RenameAsync("Files/Sample/", "Files/Some Folder").ConfigureAwait(false);
        }

        [Test, Order(7)]
        public async Task Delete()
        {
            await Client.DeleteAsync("Files/Test.md").ConfigureAwait(false);
            await Client.DeleteAsync("Files/Some Folder/").ConfigureAwait(false);

            Assert.ThrowsAsync<HttpRequestException>(() => Client.DeleteAsync("Files/Unknown.md").AsTask());
            await Client.DeleteAsync("Files/Unknown.md", true).ConfigureAwait(false);
            Assert.ThrowsAsync<HttpRequestException>(() => Client.DeleteAsync("Files/Some Unknown Folder/", true).AsTask());
        }


        [Test, Order(8)]
        public async Task PathTooLong()
        {
            await Client.CreateDirectoryAsync(Path.Combine("Files", "Wolahlegelsteinhausenbergerdorff",
                "黃宏成台灣阿成世界偉人財神總統",
                "Muvaffakiyetsizleştiricileştiriveremeyeileceklerimizdenmişsinizcesine",
                "กรุงเทพมหานคร อมรรัตนโกสินทร์ มหินทรายุทธยา นพรัตนราชธานีบุรีรมย์ อุดมราชนิเวศน์มหาสถาน อมรพิมานอวตารสถิต สักกะทัตติยะวิษณุกรรมประสิทธิ์"))
                .ConfigureAwait(false);

            using var empty = new MemoryStream(0);
            await Client.WriteFileAsync(Path.Combine("Files", "Wolfeschlegelsteinhausenbergerdorff", "Wolfeschlegelsteinhausenbergerdorff",
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


        [Test, Order(9)]
        public void ConcurrentAccess()
        {
            // Todo: Can HttpClient send multiple requests at once? Multiple client needed?
            var clients = new List<Task>();

            foreach (var i in Enumerable.Range(1, 100))
            {
                clients.Add(Client.CreateDirectoryAsync($"Files/Folder {i}").AsTask());
            }
            Task.WaitAll(clients.ToArray());
        }

    }
}
