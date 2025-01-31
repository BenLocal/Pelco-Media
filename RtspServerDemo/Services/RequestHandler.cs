﻿using Pelco.Media.RTSP;
using Pelco.Media.RTSP.SDP;
using Pelco.Media.RTSP.Server;
using RtspServerDemo.Sources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RtspServerDemo.Services
{
    public class RequestHandler : DefaultRequestHandler
    {
        private int _currentChannel = 0;
        private RtspSource _rtspSource;

        public RequestHandler(RtspSource rtspSource)
        {
            _rtspSource = rtspSource;
            _rtspSource.Initialize();
        }

        public override RtspResponse SetUp(RtspRequest request)
        {
            // create session
            var builder = RtspResponse.CreateBuilder().Status(RtspResponse.Status.Ok);

            var transport = request.Transport;
            if (transport == null)
            {
                return builder.Status(RtspResponse.Status.BadRequest).Build();
            }
            else if (transport.Type != TransportType.RtspInterleaved)
            {
                return builder.Status(RtspResponse.Status.UnsupportedTransport).Build();
            }

            lock (this)
            {
                PortPair channels = new PortPair(_currentChannel, _currentChannel + 1);
                _currentChannel += 2;

                IRtspSession session = null;
                if (request.Session != null)
                {
                    session = _sessionManager.GetSession(request.Session);
                }

                if (session == null)
                {
                    session = new RtspInterleavedSession(_rtspSource);
                    (session as RtspInterleavedSession).AddRtspRequest(request, channels);
                    _sessionManager.RegisterSession(session);
                }
                else
                {
                    (session as RtspInterleavedSession).AddRtspRequest(request, channels);
                }

                transport = TransportHeader.CreateBuilder()
                                           .Type(TransportType.RtspInterleaved)
                                           .InterleavedChannels(channels)
                                           .Build();

                return builder.AddHeader(RtspHeaders.Names.TRANSPORT, transport.ToString())
                              .AddHeader(RtspHeaders.Names.SESSION, session.Id)
                              .Build();
            }
        }

        protected override SessionDescription CreateSDP(RtspRequest request)
        {
            // set sdp
            return _rtspSource.SessionDescription();
        }
    }
}
