using Pelco.Media.Common;
using Pelco.Media.Pipeline.Transforms;
using Pelco.Media.Pipeline;
using Pelco.Media.RTSP;
using Pelco.Media.RTSP.Client;
using System;
using System.Text;

namespace RtspClientDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            //var uri = new Uri("rtsp://10.1.72.222:554/h264/ch33/main/av_stream");
            var uri = new Uri("rtsp://34.227.104.115/vod/mp4:BigBuckBunny_115k.mov");
            //var creds = new Credentials("admin", "qq111111");
            //using var client = new RtspClient(uri, creds);
            using var client = new RtspClient(uri);
            // send OPTIONS client
            var response = CheckResponse(client.Request().Options());
            Console.WriteLine($"OPTIONS : {response.Headers["Public"]}");

            // send DESCRIBE client
            response = CheckResponse(client.Request().Describe());
            var sdp = response.GetBodyAsSdp();
            Console.WriteLine($"SDP : {sdp.ToString()}");

            var filter = MimeType.H264_VIDEO;
            var tracks = MediaTracks.FromSdp(sdp, uri, filter);
            if (tracks.IsEmpty)
            {
                throw new Exception("No application tracks available");
            }

            var transport = TransportHeader.CreateBuilder()
                                    .Type(TransportType.RtspInterleaved)
                                    .InterleavedChannels(0, 1)
                                    .Build();

            // get first track
            response = CheckResponse(client.Request().Uri(tracks[0].ControlUri)
                                            .Transport(transport)
                                            .SetUp());

            var session = response.Session;
            if (session == null)
            {
                throw new Exception("Server did not return session");
            }

            var pipeline = MediaPipeline.CreateBuilder()
                                  .Source(client.GetChannelSource(0)) // Create source for receiving interleaved RTP
                                  .Transform(new H264RtpDepacketizer()) // Build metadata frames if fragmented
                                  .Sink(new H264FileSink())
                                  .Build();

            // send PLAY client
            client.Request().Session(session.ID).PlayAsync((res) => { });

            Console.ReadKey();

            pipeline.Stop();

            // Send RTSP TEARDOWN to server.
            client.Request().TeardownAsync((res) => { });
        }

        private static RtspResponse CheckResponse(RtspResponse res)
        {
            var status = res.ResponseStatus;
            if (status.Code >= RtspResponse.Status.BadRequest.Code)
            {
                throw new RtspClientException($"Received response {status.Code} {status.ReasonPhrase}");
            }

            return res;
        }

        private sealed class H264FileSink : SinkBase
        {
            public override bool WriteBuffer(ByteBuffer buffer)
            {
                // Do something with the received buffer of data
                // write file
                buffer.SetPosition(0, ByteBuffer.PositionOrigin.BEGINNING);
                while (buffer.Position < buffer.Length)
                {
                    var length = buffer.ReadInt32();
                    var nal = buffer.ReadSlice(length);

                    var nalHeader = nal.ReadByte();
                    var nalType = nalHeader & 0x1F;
                    if (nalType == 7)
                    {
                        var sps = nal;
                        Console.WriteLine("sps: " + sps.ToString());
                    }
                    else if (nalType == 8)
                    {
                        nal.SetPosition(0, ByteBuffer.PositionOrigin.BEGINNING);
                        var pps = nal;
                        Console.WriteLine("pps: " + pps.ToString());
                    }
                }

                return true;
            }
        }
    }
}
