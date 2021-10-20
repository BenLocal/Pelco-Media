﻿//
// Copyright (c) 2018 Pelco. All rights reserved.
//
// This file contains trade secrets of Pelco.  No part may be reproduced or
// transmitted in any form by any means or for any purpose without the express
// written permission of Pelco.
//
using Pelco.Media.Common;
using Pelco.Media.RTSP.SDP;
using System;
using System.Net;

namespace Pelco.Media.RTSP.Client
{
    public class MediaTrack
    {
        internal MediaTrack()
        {
            ID = Guid.NewGuid().ToString();
        }

        public string ID { get; private set; }

        public Uri ControlUri { get; internal set; }

        public MimeType Type { get; internal set; }

        public IPAddress Address { get; internal set; }

        public uint Port { get; internal set; }

        public SdpRtpMap RtpMap { get; set; }

        public AFmtPMap AFmtPMap { get; internal set; }

        public static Builder CreateBuilder()
        {
            return new Builder();
        }

        public sealed class Builder
        {
            private Uri _uri;
            private uint _port;
            private MimeType _type;
            private IPAddress _address;
            private SdpRtpMap _rtpmap;
            private AFmtPMap _aFmtPMap;

            public Builder()
            {
                Clear();
            }

            public Builder Clear()
            {
                _uri = null;
                _port = 0;
                _rtpmap = null;
                _address = null;
                _type = MimeType.ANY_TYPE;
                _aFmtPMap = null;

                return this;
            }

            public Builder Uri(Uri uri)
            {
                _uri = uri;

                return this;
            }

            public Builder Type(MimeType type)
            {
                _type = type;

                return this;
            }

            public Builder RtpMap(SdpRtpMap rtpmap)
            {
                _rtpmap = rtpmap;

                return this;
            }

            public Builder AFmtPMap(AFmtPMap aFmtPMap)
            {
                _aFmtPMap = aFmtPMap;
                
                return this;
            }

            public Builder Address(IPAddress address)
            {
                _address = address;

                return this;
            }

            public Builder Port(uint port)
            {
                _port = port;

                return this;
            }

            public MediaTrack Build()
            {
                return new MediaTrack()
                {
                    Address = _address,
                    ControlUri = _uri,
                    Port = _port,
                    RtpMap = _rtpmap,
                    Type = _type,
                    AFmtPMap = _aFmtPMap
                };
            }
        }
    }
}
