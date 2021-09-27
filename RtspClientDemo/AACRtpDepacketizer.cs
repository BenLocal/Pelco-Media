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

        public AACRtpDepacketizer() : base(new TimestampDemarcator(), new MarkerDemarcator())
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

        private void ProcessRTPFrame(RtpPacket packet)
        {
            // 重置
            packet.Payload.SetPosition(0, ByteBuffer.PositionOrigin.BEGINNING);

            while (packet.Payload.Position < packet.Payload.Length)
            {
                // 开始是2bytes的AU Header长度，2bytes的AU Header内容
                if (packet.Payload.Position + 4 > packet.Payload.Length)
                {
                    break;
                }

                var auHeaderLength = (int)Math.Ceiling(packet.Payload.ReadInt16() / 8.0);
                var auHeader = packet.Payload.ReadInt16();

                // 13bits的acc帧长度，剩余3bits是acc帧delta
                var aacFrameSize = ((packet.Payload.ReadByte() << 8) + (packet.Payload.ReadByte() << 0)) >> 3;
                if (packet.Payload.Position + aacFrameSize > packet.Payload.Length)
                {
                    // 没有足够的数据
                    break;
                }

                _frame.WriteInt32(aacFrameSize);
                _frame.Write(packet.Payload.ReadSlice(aacFrameSize));
            }
        }
    }
}
