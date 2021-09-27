using Pelco.Media.RTSP.SDP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Pelco.Media.Tests.SDP
{
    public class SdpRtpMapTest
    {
        [Fact]
        public void TestAudioSdpRtpMap()
        {
            var audioSdpRtpMap = "104 mpeg4-generic/16000/1";

            var data = SdpRtpMap.Parse(audioSdpRtpMap);

            Assert.Equal(104, data.PayloadType);
            Assert.Equal("mpeg4-generic", data.EncodingName);
            Assert.Equal(16000, (int)data.ClockRate);
            Assert.Equal("1", data.EncodingParameters);

            var audioSdpRtpMap1 = "104 mpeg4-generic/16000/";

            var data1 = SdpRtpMap.Parse(audioSdpRtpMap1);
            Assert.Equal(104, data1.PayloadType);
            Assert.Equal("mpeg4-generic", data1.EncodingName);
            Assert.Equal(16000, (int)data1.ClockRate);
            Assert.True(string.IsNullOrEmpty(data1.EncodingParameters));

            var audioSdpRtpMap2 = "104 mpeg4-generic/16000";
            var data2 = SdpRtpMap.Parse(audioSdpRtpMap2);
            Assert.Equal(104, data2.PayloadType);
            Assert.Equal("mpeg4-generic", data2.EncodingName);
            Assert.Equal(16000, (int)data2.ClockRate);
            Assert.True(string.IsNullOrEmpty(data2.EncodingParameters));
        }
    }
}
