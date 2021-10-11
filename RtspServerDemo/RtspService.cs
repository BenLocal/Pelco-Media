using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Hosting;
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
                        var handler = new 
                        dispatcher.RegisterHandler("live", handler);
                        _server = new RtspServer(8554, dispatcher);
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
                }

                return Task.CompletedTask;
            }
        }
    }
}
