using Pelco.Media.Common;
using Pelco.Media.Metadata;
using Pelco.Media.Pipeline;
using Pelco.Media.Pipeline.Sinks;
using Pelco.Media.Pipeline.Transforms;
using Pelco.Media.RTP;
using Pelco.Media.RTSP;
using Pelco.Media.RTSP.Client;
using Pelco.Media.RTSP.SDP;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Policy;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Timers;
using static Pelco.Media.RTSP.PlaybackBufferStateEvent;

namespace RtspServerDemo.Sources
{
    public enum RtspState
    {
        NONE,
        PLAYING,
        PAUSED,
        STOPPED,
        INITIALIZED,
    }

    public class RtspSource : IDisposable
    {
        private readonly RtspClient _client;
        private readonly Uri _uri;
        private readonly object _lock = new object();

        private SessionDescription _description;
        private ImmutableList<MediaTrack> _tracks;
        private RtspState _state;
        private Timer _sessionRefreshTimer;
        private List<RtpSession> _sessions;
        private uint _refreshInterval;
        private int _currentChannel = 0;

        public RtspSource(string url, Credentials credentials)
        {
            _uri = new Uri(url);
            _client = new RtspClient(_uri, credentials);
            _tracks = ImmutableList.Create<MediaTrack>();
            _sessions = new List<RtpSession>();
        }

        ~RtspSource()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposeClient)
        {
            lock (_lock)
            {
                _tracks.Clear();

                _sessionRefreshTimer.Stop();
                _sessionRefreshTimer.Dispose();

                _sessions.ForEach(s => Teardown(s, andRemove: false));
                _sessions.Clear();

                if (disposeClient)
                {
                    _client?.Dispose();
                }

                _state = RtspState.NONE;
            }
        }

        public void Initialize()
        {
            if (_state != RtspState.NONE)
            {
                // Already initialized
                return;
            }

            var method = RtspRequest.RtspMethod.OPTIONS;
            var builder = RtspRequest.CreateBuilder().Uri(_uri).Method(method);

            // TODO(frank.lamar): Add check for supported operations.  Otherwise this call
            // is meaning less.
            CheckResponse(_client.Send(builder.Build()), method);

            // Send Describe to server
            method = RtspRequest.RtspMethod.DESCRIBE;
            var response = CheckResponse(_client.Send(builder.Method(method).Build()), method);
            _description = response.GetBodyAsSdp();
            _tracks = MediaTracks.FromSdp(_description, _uri);

            // Initialize our session refresh timmer.
            _sessionRefreshTimer = new Timer();
            _sessionRefreshTimer.AutoReset = true;
            _sessionRefreshTimer.Elapsed += SessionRefreshTimer_Elapsed;

            _state = RtspState.INITIALIZED;
        }

        public SessionDescription SessionDescription() => _description;

        public void Play()
        {
            _client.Request().Session(_sessions.FirstOrDefault().ID).PlayAsync((res) => { });
        }

        public void SetUp(ISink rtpSink, bool interleaved)
        {
            foreach (var track in _tracks)
            {
                SetupWithTrack(track, rtpSink, interleaved);
            }
        }

        public MediaTrack GetTrack(string controlUri)
        {
            return _tracks.FirstOrDefault(x => x.ControlUri.ToString() == controlUri);
        }

        public RtpSession SetupWithTrack(MediaTrack track, ISink rtpSink, bool interleaved)
        {
            RtpSession session = null;
            try
            {
                var cache = _sessions.FirstOrDefault(x => x.ID == track.ID);
                if (cache != null)
                {
                    return cache;
                }

                session = SetupWithTrack(track, new RtpChannelSink(++_currentChannel, rtpSink), interleaved);
                _sessions.Add(session);
                //if (!Play(session))
                //{
                //    // This session needs to be deleted because it is not longer valide due to
                //    // an RTSP redirect.
                //    session.Dispose();
                //}
                //else
                //{
                //    // We have a good session add it so we can manage it.
                //    _sessions.Add(session);
                //}
            }
            catch (Exception e)
            {
                //LOG.Error(e, $"Failed to start playing VxMetadataSource from '{_currentUri}'");

                session?.Dispose();

                throw e;
            }

            return session;
        }

        private RtpSession SetupWithTrack(MediaTrack track, RtpChannelSink sink, bool interleaved)
        {
            lock (_lock)
            {
                IRtpSource rtpSource = null;
                try
                {
                    TransportHeader transport = null;
                    if (interleaved)
                    {
                        transport = TransportHeader.CreateBuilder()
                                                   .Type(TransportType.RtspInterleaved)
                                                   .InterleavedChannels(_currentChannel -1 , _currentChannel)
                                                   .Build();
                    }
                    else
                    {
                        // TODO(frank.lamar): Add multicast support.
                        rtpSource = new RtpUdpSource(track.Address);
                        transport = TransportHeader.CreateBuilder()
                                                   .Type(TransportType.UdpUnicast)
                                                   .ClientPorts(rtpSource.RtpPort, rtpSource.RtcpPort)
                                                   .Build();
                    }

                    var response = CheckResponse(_client.Send(RtspRequest.CreateBuilder()
                                                                         .Method(RtspRequest.RtspMethod.SETUP)
                                                                         .Uri(track.ControlUri)
                                                                         .AddHeader(RtspHeaders.Names.TRANSPORT, transport.ToString())
                                                                         .Build()), RtspRequest.RtspMethod.SETUP);

                    if (!response.Headers.ContainsKey(RtspHeaders.Names.SESSION))
                    {
                        throw new RtspClientException("Rtsp SETUP response does not contain a session id");
                    }
                    var rtspSession = Session.Parse(response.Headers[RtspHeaders.Names.SESSION]);

                    transport = response.Transport;
                    if (interleaved)
                    {
                        if (transport.Type != TransportType.RtspInterleaved)
                        {
                            throw new RtspClientException($"Server does not support interleaved. Response Transport='{transport}'");
                        }

                        var channels = transport.InterleavedChannels != null ? transport.InterleavedChannels : new PortPair(0, 1);
                        sink.Channel = channels.RtpPort; // Ensure that the sink contains the correct Interleaved channel id.

                        rtpSource = new RtpInterleavedSource(_client.GetChannelSource(channels.RtpPort),
                                                             _client.GetChannelSource(channels.RtcpPort));
                    }

                    var pipeline = MediaPipeline.CreateBuilder()
                                                .Source(rtpSource.RtpSource)
                                                .TransformIf(transport.SSRC != null, new SsrcFilter(transport.SSRC))
                                                .Sink(sink)
                                                .Build();

                    var session = new RtpSession(track, rtspSession, rtpSource);
                    session.Pipelines.Add(pipeline);
                    session.Start();

                    CheckAndStartRefreshTimer(session.Session.Timeout);

                    return session;
                }
                catch (Exception e)
                {
                    if (rtpSource != null)
                    {
                        rtpSource?.Stop();
                    }

                    if (e is RtspClientException)
                    {
                        throw e;
                    }

                    throw new RtspClientException($"Unable to set up media track {track.ID}", e);
                }
            }
        }

        private void CheckAndStartRefreshTimer(uint timeoutSecs)
        {
            if (!_sessionRefreshTimer.Enabled)
            {
                _refreshInterval = timeoutSecs;

                _sessionRefreshTimer.Interval = (timeoutSecs - 5) * 1000;
                _sessionRefreshTimer.Start();
            }
            else if (timeoutSecs < _refreshInterval)
            {
                // Because it is possible to have multiple sessions for different tracks
                // we will make the refresh interval the floor of all the available sessions.
                // Having different timeouts should never happen but we will handling it because
                // you just never know.
                _refreshInterval = timeoutSecs;

                // Because the timeout value is less we need to stop the timer and adjust
                // the interval.
                _sessionRefreshTimer.Stop();
                _sessionRefreshTimer.Interval = (timeoutSecs - 3) * 1000;

                // Defensive refresh just incase the timer was just about ready to
                // elapse before we shutdown things down.  This will ensure that the
                // session doesn't expire.
                SessionRefreshTimer_Elapsed(this, null);

                _sessionRefreshTimer.Start();
            }
        }

        private void SessionRefreshTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (_lock)
            {
                if (!_sessions.Any())
                {
                    return;
                }

                // If there are multiple RTSP sessions available refreshing a single one
                // with the base (aggragate) uri should refresh them all.
                var sessionId = _sessions[0].ID;

                try
                {
                    //LOG.Info($"Refreshing metadata session at '{_currentUri}'");
                    _client.Request().Uri(_uri).Session(sessionId).GetParameterAsync((res) =>
                    {
                        if (res.ResponseStatus.Code >= RtspResponse.Status.BadRequest.Code)
                        {
                            //LOG.Error($"Failed to refresh session '{sessionId}' received {res.ResponseStatus}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    //LOG.Error($"Unable to perform session refresh on session {sessionId}, reason: {ex.Message}");
                }
            }
        }

        private RtspResponse CheckResponse(RtspResponse res, RtspRequest.RtspMethod method)
        {
            var status = res.ResponseStatus;
            if (status.Code >= RtspResponse.Status.BadRequest.Code)
            {
                throw new RtspClientException($"Received response {status.Code} {status.ReasonPhrase}");
            }

            return res;
        }

        private void Teardown(RtpSession session, bool andRemove = true)
        {
            try
            {
                //LOG.Debug($"Tearing RTSP session '{session.ID}' at '{session.Track.ControlUri}'");

                _client.Request().Uri(_uri).Session(session.ID).TeardownAsync((res) =>
                {
                    if (res.ResponseStatus.Code >= RtspResponse.Status.BadRequest.Code)
                    {
                        //LOG.Error($"Failed to teardown session '{session.ID}' received {res.ResponseStatus}");
                    }
                });
            }
            catch (Exception e)
            {
                //LOG.Error($"Failed to Teardown session '{session.ID}' for {session.Track.ControlUri}, reason: {e.Message}");
            }
            finally
            {
                session.Dispose();

                if (andRemove)
                {
                    _sessions.Remove(session); // Remove from the list of sessions.
                }
            }
        }

        // Class used to append the channel ID to the buffer as well as mux
        // streams together if the source contains multiple streams of the
        // requested metadadta type.
        private sealed class RtpChannelSink : TransformBase
        {

            public RtpChannelSink(int channel, ISink rtpSink)
            {
                DownstreamLink = rtpSink;
                rtpSink.UpstreamLink = this;
                Channel = channel;
            }

            public int Channel { get; set; }

            public new void Stop()
            {
                base.Stop();
            }

            public override bool WriteBuffer(ByteBuffer buffer)
            {
                buffer.Channel = Channel; // Sets the buffer's channel

                return PushBuffer(buffer);
            }
        }
    }

}
