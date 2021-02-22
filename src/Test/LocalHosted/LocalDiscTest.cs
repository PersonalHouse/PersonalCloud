using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Microsoft.Extensions.Logging;

using NSPersonalCloud.LocalDiscovery;

using NUnit.Framework;

namespace LocalHosted
{
    public class LocalDiscTest
    {

        [Test]
        public void SimpleCreate()
        {
            using (var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddFile("Logs/{Date}.txt")))
            {
                var ran = new Random();
                var id = Guid.NewGuid().ToString("");
                var net = new LocalNodeRecords(loggerFactory);

                var p = ran.Next(10000, 60000);
                net.Start(p, id);
            }
            Thread.Sleep(1000000);
        }

    }


}
