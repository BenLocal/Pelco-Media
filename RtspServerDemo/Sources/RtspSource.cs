using Pelco.Media.Common;
using Pelco.Media.Pipeline;
using Pelco.Media.RTSP;
using Pelco.Media.RTSP.Client;
using Pelco.Media.RTSP.SDP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;

namespace RtspServerDemo.Sources
{
    public class RtspSource
    {
        private readonly RtspClient _client;
        private readonly Uri _uri;

        private SessionDescription _description;
        private List<TrackInfo> _tracks;
        private int _currentChannel = 0;
        private Session _session;

        public RtspSource(string url, Credentials credentials)
        {
            _uri = new Uri(url);
            _client = new RtspClient(_uri, credentials);
            _tracks = new List<TrackInfo>();
        }

        public SessionDescription GetSessionDescription()
        {
            if (_description == null)
            {

                CheckResponse(_client.Request().Options());

                var response = CheckResponse(_client.Request().Describe());
                var sdp = response.GetBodyAsSdp();

                var tracks = MediaTracks.FromSdp(sdp, _uri);
                if (tracks.IsEmpty)
                {
                    throw new Exception("No application video tracks available");
                }

                foreach (var track in tracks)
                {
                    PortPair channels = new PortPair(_currentChannel, _currentChannel + 1);
                    _currentChannel += 2;

                    var transport = TransportHeader.CreateBuilder()
                           .Type(TransportType.RtspInterleaved)
                           .InterleavedChannels(channels.RtpPort, channels.RtcpPort)
                           .Build();

                    SendSetUp(_client, track, transport, ref _session);

                    _tracks.Add(new TrackInfo()
                    {
                        MediaTrack = track,
                        TCPPortPair = channels
                    });
                }

                _description = sdp;
            }

            return _description;
        }

        public ISource GetSource(int channel)
        {
            return _client.GetChannelSource(channel);
        }

        public TrackInfo GetTrack(Uri controlUri)
        {
            return _tracks.FirstOrDefault(x => x.MediaTrack.ControlUri == controlUri);
        }

        public void Play()
        {
            _client.Request().Session(_session.ID).PlayAsync((res) => { });
        }

        private RtspResponse CheckResponse(RtspResponse res)
        {
            var status = res.ResponseStatus;
            if (status.Code >= RtspResponse.Status.BadRequest.Code)
            {
                throw new RtspClientException($"Received response {status.Code} {status.ReasonPhrase}");
            }

            return res;
        }

        private RtspResponse SendSetUp(RtspClient client, MediaTrack track, TransportHeader transport, ref Session session)
        {
            var request = client.Request();

            if (session != null)
            {
                request.Session(session.ID);
            }
            var response = CheckResponse(request.Uri(track.ControlUri)
                                           .Transport(transport)
                                           .SetUp());

            session = response.Session;
            if (session == null)
            {
                throw new Exception("Server did not return session");
            }

            return response;
        }
    }
}
