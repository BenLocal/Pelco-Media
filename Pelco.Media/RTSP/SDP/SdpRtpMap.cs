//
// Copyright (c) 2018 Pelco. All rights reserved.
//
// This file contains trade secrets of Pelco.  No part may be reproduced or
// transmitted in any form by any means or for any purpose without the express
// written permission of Pelco.
//
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Pelco.Media.RTSP.SDP
{
    /// <summary>
    /// a=rtpmap:<payload type> <encoding name>/<clock rate>[/<encoding parameters>]
    /// </summary>
    public class SdpRtpMap
    {
        //private static Regex REGEX = new Regex(@"^\s*(\d+)\s+(.+)\s*/\s*(\d+)(\s*/\s*(.+))?", RegexOptions.Compiled);

        internal SdpRtpMap()
        {

        }

        internal SdpRtpMap(ushort payloadType,
                           string encodingName,
                           uint clockRate,
                           string encodingParams = null)
        {
            PayloadType = payloadType;
            EncodingName = encodingName;
            ClockRate = clockRate;
            EncodingParameters = encodingParams;
        }

        #region Properties

        public ushort PayloadType { get; private set; }

        public string EncodingName { get; private set; }

        public uint ClockRate { get; private set; }

        public string EncodingParameters { get; private set; }

        #endregion

        public static SdpRtpMap Parse(string str)
        {
            var items = str.Split(new char[] { ' ' }, 2);
            if (items.Length != 2)
            {
                throw new SdpParseException($"Unable to parse malformed rtpmap attriute '{str}'");
            }

            var subItems = items[1].Split('/');
            if (subItems.Length != 2 && subItems.Length != 3)
            {
                throw new SdpParseException($"Unable to parse malformed rtpmap attriute '{str}'");
            }

            var builder = CreateBuilder()
                .PayloadType(ushort.Parse(items[0]))
                .EncodingName(subItems[0].Trim())
                .ClockRate(uint.Parse(subItems[1]));

            if (subItems.Length == 3)
            {
                builder.EncodingParameters(subItems[2].Trim());
            }

            return builder.Build();
        }

        public static Builder CreateBuilder()
        {
            return new Builder();
        }

        public sealed class Builder
        {
            private ushort _payloadType;
            private string _encodingName;
            private uint _clockRate;
            private string _encodingParams;

            public Builder()
            {

            }

            public Builder Clear()
            {
                _payloadType = 0;
                _encodingName = string.Empty;
                _clockRate = 0;
                _encodingParams = string.Empty;

                return this;
            }

            public Builder PayloadType(ushort payloadType)
            {
                _payloadType = payloadType;

                return this;
            }

            public Builder EncodingName(string name)
            {
                _encodingName = name;

                return this;
            }

            public Builder ClockRate(uint clockRate)
            {
                _clockRate = clockRate;

                return this;
            }

            public Builder EncodingParameters(string parameters)
            {
                _encodingParams = parameters;

                return this;
            }

            public SdpRtpMap Build()
            {
                return new SdpRtpMap()
                {
                    ClockRate = _clockRate,
                    PayloadType = _payloadType,
                    EncodingName = _encodingName,
                    EncodingParameters = _encodingParams,
                };
            }
        }
    }
}
