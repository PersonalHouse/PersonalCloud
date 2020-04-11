using System;
using System.Threading.Tasks;

namespace NSPersonalCloud.Apps.Album.ImageIndexer
{
    class Program
    {
        static async Task Main(string[] args)
        {
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
                OutputUsage();
                return;
            }
            var ts = DateTime.UtcNow.ToFileTime();
            using var idx = new ImgIndexer(indexpath);
            await idx.Scan(inputfolder, ts);
            idx.CleanNotExistImages(ts);
            await idx.SaveYearMonthDays();
        }

        private static void OutputUsage()
        {
            Console.WriteLine("ImageIndexer -O fullpathofindex.db  -I inputfolder");
        }
    }
}
