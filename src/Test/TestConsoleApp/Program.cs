using System;
using System.Globalization;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using NSPersonalCloud;

namespace TestConsoleApp
{
    class Program
    {
        static ILoggerFactory loggerFactory;
        static void Main(string[] args)
        {
            loggerFactory = LoggerFactory.Create(builder => {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("LoggingConsoleApp.Program", LogLevel.Debug).SetMinimumLevel(LogLevel.Trace)
                    .AddConsole();
            });

            Console.WriteLine("Please choose an action:");
            Console.WriteLine("1.Create and share a Personal Cloud");
            Console.WriteLine("2.Join a Personal Cloud");
            Console.WriteLine("3.List the Personal Cloud");
            Console.WriteLine("0.Exit");
            while (true)
            {
                Console.Write("Input:");
                var input = Console.ReadLine();
                Console.WriteLine();
                switch (int.Parse(input))
                {
                    case 1:
                        CreateAndSharePC();
                        break;
                    case 2:
                        JoinPersonalCloud();
                        break;
                    case 3:
                        ListContent();
                        break;
                    case 0:
                    default:
                        loggerFactory?.Dispose();
                        return;
                }
            }


        }

        private static void ListContent()
        {
            var rootfs = pc.RootFS.EnumerateChildrenAsync("/").Result;
            foreach (var item in rootfs)
            {
                Console.WriteLine($"/{item.Name}");
                if (item.Attributes.HasFlag(FileAttributes.Directory))
                {
                    ListContent($"/{item.Name}");
                }
            }
        }
        private static void ListContent(string path)
        {
            var fs = pc.RootFS.EnumerateChildrenAsync(path).Result;
            if (fs != null)
            {
                foreach (var item in fs)
                {
                    Console.WriteLine(Path.Combine(path, item.Name));
                    if (item.Attributes.HasFlag(FileAttributes.Directory))
                    {
                        ListContent(Path.Combine(path, item.Name));
                    }
                }
            }
        }
        static PCLocalService pcservice;
        static PersonalCloud pc;

        private static void JoinPersonalCloud()
        {
            var t2 = new SimpleConfigStorage(
                Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                "TestConsoleApp", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)));
            pcservice = new PCLocalService(t2, loggerFactory, new VirtualFileSystem(t2.RootPath),null);
            //pcservice.SetUdpPort(2330, new[] { 2330 });
            pcservice.StartService();

            Console.Write("Input share code:");
            var input = Console.ReadLine();
            Console.WriteLine();

            pc = pcservice.JoinPersonalCloud(int.Parse(input), "test2").Result;

        }

        private static void CreateAndSharePC()
        {
            var my = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var dic = Path.Combine(my, "TestConsoleApp", "webapps");

            var t1 = new SimpleConfigStorage(
                Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "TestConsoleApp", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)));
            pcservice = new PCLocalService(t1, loggerFactory, new VirtualFileSystem(t1.RootPath),dic);
            Directory.CreateDirectory(dic);
            pcservice.InstallApps().Wait();

            pcservice.StartService();
            pc = pcservice.CreatePersonalCloud("test", "test1").Result;


            var ret = pcservice.SharePersonalCloud(pc).Result;


            Console.WriteLine($"Share code is {ret}");
        }
    }
}
