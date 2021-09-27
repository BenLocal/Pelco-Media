using Pelco.Media.Common;
using Pelco.Media.Pipeline.Transforms;
using Pelco.Media.Pipeline;
using Pelco.Media.RTSP;
using Pelco.Media.RTSP.Client;
using System;
using System.Text;
using System.IO;

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

            var tracks = MediaTracks.FromSdp(sdp, uri);
            if (tracks.IsEmpty)
            {
                throw new Exception("No application video tracks available");
            }

            int video_data_channel = 0;
            int video_rtcp_channel = 1;
            int audio_data_channel = 2;
            int audio_rtcp_channel = 3;

            // video track and audio track
            MediaTrack audioTrack = null;
            MediaTrack videoTrack = null;
            Session session = null;
            foreach (var track in tracks)
            {
                Console.WriteLine(track.Type);

                if (track.Type.Is(MimeType.H264_VIDEO) ||
                    track.Type.Is(MimeType.AAC_MPEG4_AUDIO))
                {
                    var transport = TransportHeader.CreateBuilder()
                        .Type(TransportType.RtspInterleaved);


                    if (track.Type.Is(MimeType.H264_VIDEO))
                    {
                        videoTrack = track;
                        transport.InterleavedChannels(video_data_channel, video_rtcp_channel);
                    }
                    else if (track.Type.Is(MimeType.AAC_MPEG4_AUDIO))
                    {
                        audioTrack = track;
                        transport.InterleavedChannels(audio_data_channel, audio_rtcp_channel);
                    }

                    response = SendSetUp(client, track, transport.Build(), ref session);
                }
            }

            session = response.Session;
            if (session == null)
            {
                throw new Exception("Server did not return session");
            }

            var videoPipeline = videoTrack != null ? MediaPipeline.CreateBuilder()
                                  // Create source for receiving interleaved RTP
                                  .Source(client.GetChannelSource(video_data_channel))
                                  // Build metadata frames if fragmented
                                  .Transform(new H264RtpDepacketizer())
                                  .Sink(new H264FileSink())
                                  .Build() : null;

            var audioPipeline = audioTrack != null ? MediaPipeline.CreateBuilder()
                                  // Create source for receiving interleaved RTP
                                  .Source(client.GetChannelSource(audio_data_channel))
                                  // Build metadata frames if fragmented
                                  .Transform(new AACRtpDepacketizer())
                                  .Sink(new ACCFileSink(audioTrack.AFmtPMap?.Parameters["config"]))
                                  .Build() : null;

            // send PLAY client
            client.Request().Session(session.ID).PlayAsync((res) => { });

            Console.ReadKey();

            videoPipeline?.Stop();
            audioPipeline?.Stop();

            // Send RTSP TEARDOWN to server.
            client.Request().TeardownAsync((res) => { });
        }

        private static RtspResponse SendSetUp(RtspClient client, MediaTrack track, TransportHeader transport, ref Session session)
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

        private static RtspResponse CheckResponse(RtspResponse res)
        {
            var status = res.ResponseStatus;
            if (status.Code >= RtspResponse.Status.BadRequest.Code)
            {
                throw new RtspClientException($"Received response {status.Code} {status.ReasonPhrase}");
            }

            return res;
        }
    }
}
