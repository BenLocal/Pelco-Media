using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Hosting;
using Pelco.Media.Common;
using Pelco.Media.RTSP.Server;
using RtspServerDemo.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RtspServerDemo
{
    public class RtspService : BackgroundService
    {
        private const string GlobalMutexId = "RtspService";

        private RtspServer _server;

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var mutex = new Mutex(false, GlobalMutexId))
            {
                bool mutexAcquired = false;

                try
                {
                    // Because the tests can run on multiple threads we must synchronize
                    // to ensure that we don't start different test servers on the same port.
                    if ((mutexAcquired = mutex.WaitOne(5000, false)))
                    {
                        var dispatcher = new RtspRequestDispatcher();

                        var url = "rtsp://admin:qq111111@10.1.72.222:554/h264/ch33/main/av_stream";
                        var creds = new Credentials("admin", "qq111111");

                        var hander = new RequestHandler(new Sources.RtspSource(url, creds));

                        dispatcher.RegisterHandler("live", hander);
                        _server = new RtspServer(8557, dispatcher);
                        _server.Start();
                    }
                }
                catch (AbandonedMutexException)
                {
                    // do nothing
                }
                catch (Exception)
                {
                    // Do nothing since this is just a test, and if we fail here the tests
                    // are going to fail also.
                }
                finally
                {
                    if (mutexAcquired)
                    {
                        mutex.ReleaseMutex();
                    }

                    //_server?.Stop();
                }

                return Task.CompletedTask;
            }
        }
    }
}
