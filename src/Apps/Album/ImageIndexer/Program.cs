using System;
using System.Threading.Tasks;

namespace NSPersonalCloud.Apps.Album.ImageIndexer
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            SQLitePCL.Batteries_V2.Init();

            string indexpath = null;
            string inputfolder = null;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-O"://output
                        indexpath = args[++i];
                        break;

                    case "-I":
                        inputfolder = args[++i];
                        break;

                    default:
                        OutputUsage();
                        return;
                }
            }
            if (string.IsNullOrWhiteSpace(indexpath) || string.IsNullOrWhiteSpace(inputfolder))
            {
                Console.WriteLine($"Input indexpath is {indexpath}, inputfolder is {inputfolder}");
                Console.WriteLine("Input empty path");
                OutputUsage();
                return;
            }
            var ts = DateTime.UtcNow.ToFileTime();
            using var idx = new ImgIndexer(indexpath);
            await idx.Scan(inputfolder, ts).ConfigureAwait(false);
            idx.CleanNotExistImages(ts);
            await idx.SaveYearMonthDays().ConfigureAwait(false);
        }

        private static void OutputUsage()
        {
            Console.WriteLine("ImageIndexer -O fullpathofindex.db  -I inputfolder");
        }
    }
}
