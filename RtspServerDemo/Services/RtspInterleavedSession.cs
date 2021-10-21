using Pelco.Media.Pipeline;
using Pelco.Media.Pipeline.Sinks;
using Pelco.Media.Pipeline.Transforms;
using Pelco.Media.RTP;
using Pelco.Media.RTSP;
using Pelco.Media.RTSP.Server;
using Pelco.PDK.Media.Pipeline.Transforms;
using RtspServerDemo.Sources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RtspServerDemo.Services
{
    public class RtspInterleavedSession : RtspSessionBase
    {
        private List<RtspRequest> _requests;
        private RtspSource _rtspSource;

        public RtspInterleavedSession(RtspSource rtspSource)
        {
            _rtspSource = rtspSource;
            _requests = new List<RtspRequest>();
        }

        public void AddRtspRequest(RtspRequest rtspRequest, PortPair channels)
        {
            lock (this)
            {
                var sink = new TcpInterleavedSink(rtspRequest.Context, 
                    (byte)channels.RtpPort);
                var track = _rtspSource.GetTrack(rtspRequest.URI.ToString());

                if (track != null)
                {
                    _rtspSource.SetupWithTrack(track, sink, true);
                }

                _requests.Add(rtspRequest);
            }
        }

        public override void Start()
        {
            _rtspSource.Play();
        }

        public override void Stop()
        {
        }
    }
}
