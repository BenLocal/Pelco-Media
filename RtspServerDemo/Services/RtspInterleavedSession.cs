using Pelco.Media.Pipeline;
using Pelco.Media.Pipeline.Sinks;
using Pelco.Media.Pipeline.Transforms;
using Pelco.Media.RTP;
using Pelco.Media.RTSP;
using Pelco.Media.RTSP.Server;
using RtspServerDemo.Sources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RtspServerDemo.Services
{
    public class RtspInterleavedSession : RtspSessionBase
    {
        private PortPair _ports;
        private TrackInfo _trackInfo;
        private RequestContext _context;
        private MediaPipeline _pipeline;
        private RtspSource _rtspSource;

        public RtspInterleavedSession(RequestContext context, RtspSource rtspSource, PortPair port, TrackInfo trackInfo)
        {
            _rtspSource = rtspSource;
            _context = context ?? throw new ArgumentNullException("Context cannot be null");
            _ports = port ?? throw new ArgumentNullException("PortPair cannot be null");
            _trackInfo = trackInfo;
        }

        public override void Start()
        {
            lock (this)
            {
                if (_pipeline == null)
                {
                    _pipeline = MediaPipeline.CreateBuilder()
                                             .Source(_rtspSource.GetSource(_trackInfo.TCPPortPair.RtpPort))
                                             .Transform(new RtpPacketizer(
                                                 new DefaultRtpClock(_trackInfo.MediaTrack.RtpMap.ClockRate),
                                                 SSRC,
                                                 (byte)_trackInfo.MediaTrack.RtpMap.PayloadType))
                                             .Sink(new TcpInterleavedSink(_context, (byte)_ports.RtpPort))
                                             .Build();

                    _pipeline.Start();

                    _rtspSource.Play();
                }
            }
        }

        public override void Stop()
        {
            lock (this)
            {
                _pipeline?.Stop();
            }
        }
    }
}
