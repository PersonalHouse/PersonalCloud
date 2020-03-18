using System;
using System.Globalization;
using System.IO;
using System.Threading;

using Microsoft.Extensions.Logging;

using NUnit.Framework;

using NSPersonalCloud;
using System.Threading.Tasks;
using EmbedIO;

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
                    loggerFactory, new VirtualFileSystem(inf.GetConfigFolder())))
                {
                    srv.StartService();
                    var pc = srv.CreatePersonalCloud("test", "testfolder").Result;
                    Thread.Sleep(1000);
                    var lis = pc.RootFS.EnumerateChildrenAsync("/").Result;
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
                 loggerFactory, new VirtualFileSystem(inf1.GetConfigFolder())))
                {
                    var inf2 = new HostPlatformInfo();
                    using (var srv2 = new PCLocalService(inf2,
                    loggerFactory, new VirtualFileSystem(inf2.GetConfigFolder())))
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
                 loggerFactory, new VirtualFileSystem(inf1.GetConfigFolder())))
                {
                    srv1.SetUdpPort(nport1, new[] { nport2, nport1 });
                    srv1.StartService();
                    var pc1 = srv1.CreatePersonalCloud("test", "test1").Result;
                    var ret = srv1.SharePersonalCloud(pc1).Result;

                    Thread.Sleep(1000);
                    
                    var inf2 = new HostPlatformInfo();
                    using (var srv2 = new PCLocalService(inf2,
                    loggerFactory, new VirtualFileSystem(inf2.GetConfigFolder())))
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
                    srv[i] = new PCLocalService(inf[i], loggerFactory, new VirtualFileSystem(inf[i].GetConfigFolder()));
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
                Thread.Sleep(3000);

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

        private void SimapleShareCheckContent(PersonalCloud pc, int expectedCount, int nodes)
        {
            var fs2 = pc.RootFS.EnumerateChildrenAsync("/").Result;
            Assert.AreEqual(nodes, fs2.Count);
            foreach (var item in fs2)
            {
                var f = pc.RootFS.EnumerateChildrenAsync($"/{item.Name}").Result;
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
                    loggerFactory, new VirtualFileSystem(inf.GetConfigFolder())))
                {
                    srv.StartService();
                    var pc = srv.CreatePersonalCloud("test", "testfolder").Result;
                    Thread.Sleep(1000);
                    for (int i = 0; i < 100; i++)
                    {
                        var lis = pc.RootFS.EnumerateChildrenAsync("/").Result;
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
                    loggerFactory, new VirtualFileSystem(inf.GetConfigFolder())))
                {
                    srv.StartService();
                    var pc = srv.CreatePersonalCloud("test", "testfolder").Result;
                    Thread.Sleep(1000);
                    for (int i = 0; i < 10; i++)
                    {
                        var lis = pc.RootFS.EnumerateChildrenAsync("/").Result;
                        Assert.AreEqual(1, lis.Count);
                        srv.StartNetwork(false);
                        Thread.Sleep(200);
                    }
                    for (int i = 0; i < 10; i++)
                    {
                        var lis = pc.RootFS.EnumerateChildrenAsync("/").Result;
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
                 loggerFactory, new VirtualFileSystem(inf1.GetConfigFolder())))
                {
                    srv1.SetUdpPort(nport1, new[] { nport2, nport1 });
                    srv1.StartService();
                    var pc1 = srv1.CreatePersonalCloud("test", "test1").Result;
                    var ret = srv1.SharePersonalCloud(pc1).Result;

                    Thread.Sleep(1000);

                    var inf2 = new HostPlatformInfo();
                    using (var srv2 = new PCLocalService(inf2,
                    loggerFactory, new VirtualFileSystem(inf2.GetConfigFolder())))
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
                 loggerFactory, new VirtualFileSystem(inf1.GetConfigFolder())))
                {
                    srv1.TestSetReannounceTime(3 * 1000);
                    srv1.SetUdpPort(nport1, new[] { nport2, nport1 });
                    srv1.StartService();
                    var pc1 = srv1.CreatePersonalCloud("test", "test1").Result;
                    var ret = srv1.SharePersonalCloud(pc1).Result;

                    Thread.Sleep(1000);

                    var inf2 = new HostPlatformInfo();
                    using (var srv2 = new PCLocalService(inf2,
                    loggerFactory, new VirtualFileSystem(inf2.GetConfigFolder())))
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
                        _= pc1.RootFS.EnumerateChildrenAsync("/").Result;
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
