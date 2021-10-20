using Pelco.Media.RTSP;
using Pelco.Media.RTSP.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RtspServerDemo.Sources
{
    public class TrackInfo
    {
        public MediaTrack MediaTrack { get; set; }

        public PortPair TCPPortPair { get; set; }
    }
}
