using NLog;
using Pelco.Media.Pipeline.Transforms;
using Pelco.Media.Pipeline;
using Pelco.Media.RTP;
using System;
using System.Collections.Generic;
using System.Text;

namespace RtspClientDemo
{
    public class AACRtpDepacketizer : RtpDepacketizerBase, IDisposable
    {
        private static readonly Logger LOG = LogManager.GetCurrentClassLogger();

        private bool _disposed;
        private ByteBuffer _frame;
        private bool _processingFragment;
        private ushort _expectedNextSeqNum;

        public AACRtpDepacketizer() : base(new TimestampDemarcator(), new AlwaysTrueDemarcator())
        {
            _disposed = false;
            _frame = new ByteBuffer();
            _processingFragment = false;
            _expectedNextSeqNum = 0;
        }

        ~AACRtpDepacketizer()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _frame?.Dispose();
                }
            }
        }

        protected override void addRtpPacket(RtpPacket packet)
        {
            ushort seqNum = packet.SequenceNumber;

            if (_processingFragment)
            {
                if (_expectedNextSeqNum != seqNum)
                {
                    LOG.Debug($"Lost packet expected sequence number '{_expectedNextSeqNum}' got '{seqNum}'");
                    _processingFragment = false;
                    IsDamaged = true;
                }
                else
                {
                    //_frame.Write(packet.Payload);
                    ProcessRTPFrame(packet);
                }
            }
            else if (IsDamaged)
            {
                LOG.Debug($"Disgarding fragment '{seqNum}' from damaged frame");
            }
            else
            {
                _processingFragment = true;
                //_frame.Write(packet.Payload);
                ProcessRTPFrame(packet);
            }

            _expectedNextSeqNum = ++seqNum;
        }

        protected override ByteBuffer Assemble()
        {
            var assembled = _frame;
            _frame = new ByteBuffer();
            _processingFragment = false;
            IsDamaged = false;

            assembled.MarkReadOnly();
            return assembled;
        }

        // rfc3640 2.11.  Global Structure of Payload Format
        //
        // +---------+-----------+-----------+---------------+
        // | RTP     | AU Header | Auxiliary | Access Unit   |
        // | Header  | Section   | Section   | Data Section  |
        // +---------+-----------+-----------+---------------+
        //
        //           <----------RTP Packet Payload----------->
        //
        // rfc3640 3.2.1.  The AU Header Section
        //
        // +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+- .. -+-+-+-+-+-+-+-+-+-+
        // |AU-headers-length|AU-header|AU-header|      |AU-header|padding|
        // |                 |   (1)   |   (2)   |      |   (n)   | bits  |
        // +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+- .. -+-+-+-+-+-+-+-+-+-+
        //
        // rfc3640 3.3.6.  High Bit-rate AAC
        //
        // rtp_parse_mp4_au()
        //
        //
        // 3.2.3.1.  Fragmentation
        //
        //   A packet SHALL carry either one or more complete Access Units, or a
        //   single fragment of an Access Unit.  Fragments of the same Access Unit
        //   have the same time stamp but different RTP sequence numbers.  The
        //   marker bit in the RTP header is 1 on the last fragment of an Access
        //   Unit, and 0 on all other fragments.
        //
        private void ProcessRTPFrame(RtpPacket packet)
        {
            // 重置
            packet.Payload.SetPosition(0, ByteBuffer.PositionOrigin.BEGINNING);

            while (packet.Payload.Position < packet.Payload.Length)
            {
                // 开始是2bytes的AU Header长度
                // 2 bytes of AU Header payload
                if (packet.Payload.Position + 4 > packet.Payload.Length)
                {
                    return;
                }

                // AU-headers-length固定两个字节的长度,单位是bits
                var auHeadersLengthBites = (((packet.Payload.ReadByte() << 8) + (packet.Payload.ReadByte() << 0)));
                // AU-headers-length的byte长度
                var auHeadersLength = (int)Math.Ceiling((double)auHeadersLengthBites / 8.0);

                // 这里的2是写死的，正常是外部传入auSize和auIndex所占位数的和
                // auSize和auIndex所在的位数是写死的13bit，3bit，标准的做法应该从外部传入，比如从sdp中获取后传入
                var auHeaderSize = 2;
                // 有多少个AU-Header
                var nbAuHeaders = auHeadersLength / auHeaderSize;

                int offset = packet.Payload.Position + auHeadersLength;
                for (var i = 0; i < nbAuHeaders; i++)
                {
                    var firstBits = packet.Payload.ReadByte();
                    var sceondBits = packet.Payload.ReadByte();
                    // 13bits的acc帧长度，剩余3bits是acc帧delta
                    var auSize = ((firstBits << 8) + (sceondBits << 0)) >> 3;
                    // aac_index
                    int _ = sceondBits & 0x03; // 3 bits

                    if (offset + auSize > packet.Payload.Length)
                    {
                        // 没有足够的数据
                        break;
                    }

                    _frame.WriteInt32(auSize);
                    _frame.Write(packet.Payload.Slice(offset, auSize));
                    offset += auSize;
                }
            }
        }
    }
}
