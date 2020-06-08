using System;
using System.Globalization;
using System.IO;
using System.Threading;

using Microsoft.Extensions.Logging;

using NUnit.Framework;

using NSPersonalCloud;
using System.Threading.Tasks;
using EmbedIO;
using NSPersonalCloud.Apps.Album;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace LocalHosted
{
#pragma warning disable CA1303 // Do not pass literals as localized parameters
    public class LocalServiceTest
    {
        [Test]
        public void SimpleCreate()
        {
            using (var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddFile("Logs/{Date}.txt")))
            {
                var inf = new HostPlatformInfo();
                using (var srv = new PCLocalService(inf,
                    loggerFactory, new VirtualFileSystem(inf.GetConfigFolder()), null))
                {
                    srv.StartService();
                    var pc = srv.CreatePersonalCloud("test", "testfolder").Result;
                    Thread.Sleep(1000);
                    var lis = pc.RootFS.EnumerateChildrenAsync("/").AsTask().Result;
                    Assert.AreEqual( 1, lis.Count);
                }
            }
        }

        [Test]
        public void SimpleShare()
        {

            using (var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddFile("Logs/{Date}.txt",LogLevel.Trace)))
            {
                var l = loggerFactory.CreateLogger<LocalServiceTest>();
                var t = DateTime.Now;

                var inf1 = new HostPlatformInfo();
                using (var srv1 = new PCLocalService(inf1,
                 loggerFactory, new VirtualFileSystem(inf1.GetConfigFolder()), null))
                {
                    var inf2 = new HostPlatformInfo();
                    using (var srv2 = new PCLocalService(inf2,
                    loggerFactory, new VirtualFileSystem(inf2.GetConfigFolder()), null))
                    {
                        srv1.StartService();
                        srv2.StartService();

                        //l.LogInformation((DateTime.Now - t).TotalSeconds.ToString());
                        var pc1 = srv1.CreatePersonalCloud("test", "test1").Result;

                        var ret = srv1.SharePersonalCloud(pc1).Result;
                        Thread.Sleep(3000);
                        var pc2 = srv2.JoinPersonalCloud(int.Parse(ret, CultureInfo.InvariantCulture), "test2").Result;
                        Thread.Sleep(1000);

                        SimapleShareCheckContent(pc2, 2,2);
                        SimapleShareCheckContent(pc1, 2,2);
                    }
                }
            }
        }


        [Test]
        public async Task SimpleApp()
        {

            var my = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var dic = Path.Combine(my, "TestConsoleApp", "webapps");

            var t1 = new SimpleConfigStorage(
                Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "TestConsoleApp", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(dic);


            using (var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddFile("Logs/{Date}.txt", LogLevel.Trace)))
            {
                var l = loggerFactory.CreateLogger<LocalServiceTest>();
                var t = DateTime.Now;

                var inf1 = new HostPlatformInfo();
                using (var srv1 = new PCLocalService(t1, loggerFactory, new VirtualFileSystem(t1.RootPath), dic))
                {
                    srv1.InstallApps().Wait();

                    var inf2 = new HostPlatformInfo();
                    using (var srv2 = new PCLocalService(inf2, loggerFactory, new VirtualFileSystem(inf2.GetConfigFolder()), null))
                    {
                        srv1.StartService();
                        srv2.StartService();

                        //l.LogInformation((DateTime.Now - t).TotalSeconds.ToString());
                        var pc1 = srv1.CreatePersonalCloud("test", "test1").Result;

                        var strcfig = JsonConvert.SerializeObject(new List<AlbumConfig>() {
                            new AlbumConfig {
                                MediaFolder= @"F:\pics",
                                Name="test",
                                ThumbnailFolder=@"D:\Projects\out"
                            } });
                        await srv1.SetAppMgrConfig("Album", pc1.Id, strcfig).ConfigureAwait(false);

                        Assert.AreEqual(pc1.Apps?.Count, 1);

                        var ret = srv1.SharePersonalCloud(pc1).Result;
                        Thread.Sleep(3000);
                        var pc2 = srv2.JoinPersonalCloud(int.Parse(ret, CultureInfo.InvariantCulture), "test2").Result;
                        Thread.Sleep(1000);

                        Assert.AreEqual(pc2.Apps?.Count, 1);
                        foreach (var item in pc2.Apps)
                        {
                            var url = pc2.GetWebAppUri(item);
                            if (string.IsNullOrWhiteSpace(url?.AbsoluteUri))
                            {
                                Assert.Fail();
                            }
                        }
                    }
                }
            }
        }


        [Test]
        public async Task SimpleAppinFS()
        {

            var my = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var dic = Path.Combine(my, "TestConsoleApp", "webapps");

            var t1 = new SimpleConfigStorage(
                Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "TestConsoleApp", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(dic);


            using (var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddFile("Logs/{Date}.txt", LogLevel.Trace)))
            {
                var l = loggerFactory.CreateLogger<LocalServiceTest>();
                var t = DateTime.Now;

                var inf1 = new HostPlatformInfo();
                using (var srv1 = new PCLocalService(t1, loggerFactory, new VirtualFileSystem(t1.RootPath), dic))
                {
                    srv1.InstallApps().Wait();

                    var inf2 = new HostPlatformInfo();
                    using (var srv2 = new PCLocalService(inf2, loggerFactory, new VirtualFileSystem(inf2.GetConfigFolder()), null))
                    {
                        srv1.StartService();
                        srv2.StartService();

                        //l.LogInformation((DateTime.Now - t).TotalSeconds.ToString());
                        var pc1 = srv1.CreatePersonalCloud("test", "test1").Result;


                        var strcfig = JsonConvert.SerializeObject(new List<AlbumConfig>() {
                            new AlbumConfig {
                                MediaFolder= @"F:\pics",
                                Name="test",
                                ThumbnailFolder=@"D:\Projects\out"
                            } });
                        await srv1.SetAppMgrConfig("Album", pc1.Id, strcfig).ConfigureAwait(false);


                        Assert.AreEqual(pc1.Apps?.Count, 1);

                        var ret = srv1.SharePersonalCloud(pc1).Result;
                        Thread.Sleep(3000);
                        var pc2 = srv2.JoinPersonalCloud(int.Parse(ret, CultureInfo.InvariantCulture), "test2").Result;
                        Thread.Sleep(1000);

                        Assert.AreEqual(pc2.Apps?.Count, 1);
                        foreach (var item in pc2.Apps)
                        {
                            var url = pc2.GetWebAppUri(item);
                            if (string.IsNullOrWhiteSpace(url?.AbsoluteUri))
                            {
                                Assert.Fail();
                            }
                        }

                        var appinfs = new NSPersonalCloud.FileSharing.AppInFs();
                        appinfs.GetApps = () => pc1.Apps;
                        appinfs.GetUrl = (x) => pc1.GetWebAppUri(x).ToString();
                        var ls = await appinfs.EnumerateChildrenAsync("/").ConfigureAwait(false);
                        Assert.AreEqual(ls.Count, 1);
                    }
                }
            }
        }

        [Test]
        public void SimpleShare2()
        {

            using (var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddFile("Logs/{Date}.txt", LogLevel.Trace)))
            {
                var l = loggerFactory.CreateLogger<LocalServiceTest>();
                var t = DateTime.Now;

                var ran = new Random();
                int nport1 = ran.Next(1000, 10000);
                int nport2 = ran.Next(1000, 10000);

                l.LogInformation($"port 1 is {nport1}  port 2 is {nport2}");
                var inf1 = new HostPlatformInfo();
                using (var srv1 = new PCLocalService(inf1,
                 loggerFactory, new VirtualFileSystem(inf1.GetConfigFolder()), null))
                {
                    srv1.SetUdpPort(nport1, new[] { nport2, nport1 });
                    srv1.StartService();
                    var pc1 = srv1.CreatePersonalCloud("test", "test1").Result;
                    var ret = srv1.SharePersonalCloud(pc1).Result;

                    Thread.Sleep(1000);
                    
                    var inf2 = new HostPlatformInfo();
                    using (var srv2 = new PCLocalService(inf2,
                    loggerFactory, new VirtualFileSystem(inf2.GetConfigFolder()), null))
                    {
                        srv2.SetUdpPort(nport2, new[] { nport2, nport1 });
                        l.LogInformation($"before srv2.StartService(),port {srv2.ServerPort}");
                        srv2.StartService();

                        //l.LogInformation((DateTime.Now - t).TotalSeconds.ToString());
                        Thread.Sleep(1000);
                        l.LogInformation("before srv2.JoinPersonalCloud();");
                        var pc2 = srv2.JoinPersonalCloud(int.Parse(ret, CultureInfo.InvariantCulture), "test2").Result;
                        Thread.Sleep(1000);

                        SimapleShareCheckContent(pc2, 2, 2);
                        SimapleShareCheckContent(pc1, 2, 2);
                    }
                }
            }
        }

        [Test]
        public void ShareToMultiple()
        {
            int count = 100;
            using (var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddFile("Logs/{Date}.txt", LogLevel.Trace)))
            {
                var l = loggerFactory.CreateLogger<LocalServiceTest>();
                var t = DateTime.Now;

                var inf = new HostPlatformInfo[count];
                var srv = new PCLocalService[count];
                var ports = new int[count];
                var pcs = new PersonalCloud[count];
                for (int i = 0; i < count; i++)
                {
                    inf[i] = new HostPlatformInfo();
                    srv[i] = new PCLocalService(inf[i], loggerFactory, new VirtualFileSystem(inf[i].GetConfigFolder()), null);
                    ports[i] = 2000 + i;
                }

                Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = 3 },
                    i => {
                        srv[i].SetUdpPort(ports[i], ports);
                        srv[i].StartService();
                    });

                pcs[0] = srv[0].CreatePersonalCloud("test", "test0").Result;
                var ret = srv[0].SharePersonalCloud(pcs[0]).Result;
                l.LogInformation("srv0 is sharing");
                Thread.Sleep(10000);

                Parallel.For(1, count,new ParallelOptions { MaxDegreeOfParallelism=2},
                    i => {
                    pcs[i] = srv[i].JoinPersonalCloud(int.Parse(ret, CultureInfo.InvariantCulture), $"test{i}").Result;
                });
                Thread.Sleep(300 * count);

                for (int i = 0; i < count; i++)
                {
                    SimapleShareCheckContent(pcs[i], 2, count);
                }

            }
        }

        static private void SimapleShareCheckContent(PersonalCloud pc, int expectedCount, int nodes)
        {
            var fs2 = pc.RootFS.EnumerateChildrenAsync("/").AsTask().Result;
            Assert.AreEqual(nodes, fs2.Count);
            foreach (var item in fs2)
            {
                var f = pc.RootFS.EnumerateChildrenAsync($"/{item.Name}").AsTask().Result;
                Assert.AreEqual(expectedCount, f.Count);
            }
        }


        [Test]
        public void TestRepublish()
        {
            using (var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddFile("Logs/{Date}.txt")))
            {
                var inf = new HostPlatformInfo();
                using (var srv = new PCLocalService(inf,
                    loggerFactory, new VirtualFileSystem(inf.GetConfigFolder()), null))
                {
                    srv.StartService();
                    var pc = srv.CreatePersonalCloud("test", "testfolder").Result;
                    Thread.Sleep(1000);
                    for (int i = 0; i < 100; i++)
                    {
                        var lis = pc.RootFS.EnumerateChildrenAsync("/").AsTask().Result;
                        Assert.AreEqual(1, lis.Count);
                        srv.StopNetwork();
                        Thread.Sleep(100);
                        srv.StartNetwork(true);
                        Thread.Sleep(200);
                    }
                }
            }
        }



#if DEBUG
        [Test]
        public void TestRepublishwithoutStop()
        {
            using (var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddFile("Logs/{Date}.txt")))
            {
                var inf = new HostPlatformInfo();
                using (var srv = new PCLocalService(inf,
                    loggerFactory, new VirtualFileSystem(inf.GetConfigFolder()), null))
                {
                    srv.StartService();
                    var pc = srv.CreatePersonalCloud("test", "testfolder").Result;
                    Thread.Sleep(1000);
                    for (int i = 0; i < 10; i++)
                    {
                        var lis = pc.RootFS.EnumerateChildrenAsync("/").AsTask().Result;
                        Assert.AreEqual(1, lis.Count);
                        srv.StartNetwork(false);
                        Thread.Sleep(200);
                    }
                    for (int i = 0; i < 10; i++)
                    {
                        var lis = pc.RootFS.EnumerateChildrenAsync("/").AsTask().Result;
                        Assert.AreEqual(1, lis.Count);
                        srv.TestStopWebServer();
                        srv.StartNetwork(false);
                        Thread.Sleep(200);
                    }
                }
            }
        }
#endif//DEBUG

        [Test]
        public void TestStopNetwork()
        {
            using (var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddFile("Logs/{Date}.txt", LogLevel.Trace)))
            {
                var l = loggerFactory.CreateLogger<LocalServiceTest>();
                var t = DateTime.Now;

                var ran = new Random();
                int nport1 = ran.Next(1000, 10000);
                int nport2 = ran.Next(1000, 10000);

                l.LogInformation($"port 1 is {nport1}  port 2 is {nport2}");
                var inf1 = new HostPlatformInfo();
                using (var srv1 = new PCLocalService(inf1,
                 loggerFactory, new VirtualFileSystem(inf1.GetConfigFolder()), null))
                {
                    srv1.SetUdpPort(nport1, new[] { nport2, nport1 });
                    srv1.StartService();
                    var pc1 = srv1.CreatePersonalCloud("test", "test1").Result;
                    var ret = srv1.SharePersonalCloud(pc1).Result;

                    Thread.Sleep(1000);

                    var inf2 = new HostPlatformInfo();
                    using (var srv2 = new PCLocalService(inf2,
                    loggerFactory, new VirtualFileSystem(inf2.GetConfigFolder()), null))
                    {
                        srv2.SetUdpPort(nport2, new[] { nport2, nport1 });
                        l.LogInformation($"before srv2.StartService(),port {srv2.ServerPort}");
                        srv2.StartService();

                        //l.LogInformation((DateTime.Now - t).TotalSeconds.ToString());
                        Thread.Sleep(1000);
                        l.LogInformation("before srv2.JoinPersonalCloud();");
                        var pc2 = srv2.JoinPersonalCloud(int.Parse(ret, CultureInfo.InvariantCulture), "test2").Result;
                        Thread.Sleep(1000);

                        SimapleShareCheckContent(pc2, 2, 2);
                        SimapleShareCheckContent(pc1, 2, 2);

                        srv2.StopNetwork();
                        SimapleShareCheckContent(pc2, 0, 0);
                        srv2.StartNetwork(true);
                        Thread.Sleep(3000);
                        SimapleShareCheckContent(pc2, 2, 2);
                    }
                }
            }
        }

#if DEBUG
        [Test]
        public void TestExpiredNodes()
        {
            using (var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddFile("Logs/{Date}.txt", LogLevel.Trace)))
            {
                var l = loggerFactory.CreateLogger<LocalServiceTest>();
                var t = DateTime.Now;

                var ran = new Random();
                int nport1 = ran.Next(1000, 10000);
                int nport2 = ran.Next(1000, 10000);

                l.LogInformation($"port 1 is {nport1}  port 2 is {nport2}");
                var inf1 = new HostPlatformInfo();
                using (var srv1 = new PCLocalService(inf1,
                 loggerFactory, new VirtualFileSystem(inf1.GetConfigFolder()), null))
                {
                    srv1.TestSetReannounceTime(3 * 1000);
                    srv1.SetUdpPort(nport1, new[] { nport2, nport1 });
                    srv1.StartService();
                    var pc1 = srv1.CreatePersonalCloud("test", "test1").Result;
                    var ret = srv1.SharePersonalCloud(pc1).Result;

                    Thread.Sleep(1000);

                    var inf2 = new HostPlatformInfo();
                    using (var srv2 = new PCLocalService(inf2,
                    loggerFactory, new VirtualFileSystem(inf2.GetConfigFolder()), null))
                    {
                        srv2.TestSetReannounceTime(3 * 1000);
                        srv2.SetUdpPort(nport2, new[] { nport2, nport1 });
                        l.LogInformation($"before srv2.StartService(),port {srv2.ServerPort}");
                        srv2.StartService();

                        //l.LogInformation((DateTime.Now - t).TotalSeconds.ToString());
                        Thread.Sleep(1000);
                        l.LogInformation("before srv2.JoinPersonalCloud();");
                        var pc2 = srv2.JoinPersonalCloud(int.Parse(ret, CultureInfo.InvariantCulture), "test2").Result;
                        Thread.Sleep(1000);

                        SimapleShareCheckContent(pc2, 2, 2);
                        SimapleShareCheckContent(pc1, 2, 2);

                        srv2.StopNetwork();
                        srv2.Dispose();
                        Thread.Sleep(10000);
                        _= pc1.RootFS.EnumerateChildrenAsync("/").AsTask().Result;
                        Thread.Sleep(1000);
                        SimapleShareCheckContent(pc1, 2, 1);
                    }
                }
            }
        }
#endif//DEBUG
    }
#pragma warning restore CA1303 // Do not pass literals as localized parameters
}
