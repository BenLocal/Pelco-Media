using NLog;
using Pelco.Media.Pipeline;
using Pelco.Media.Pipeline.Transforms;
using Pelco.Media.RTP;
using System;
using System.Collections.Generic;
using System.Text;

namespace RtspClientDemo
{
    public class H264RtpDepacketizer : RtpDepacketizerBase, IDisposable
    {
        private static readonly Logger LOG = LogManager.GetCurrentClassLogger();

        private bool _disposed;
        private ByteBuffer _frame;
        private bool _processingFragment;
        private ushort _expectedNextSeqNum;
        private ByteBuffer _fragmented_nal;

        public H264RtpDepacketizer() : base(new TimestampDemarcator(), new MarkerDemarcator())
        {
            _disposed = false;
            _frame = new ByteBuffer();
            _processingFragment = false;
            _expectedNextSeqNum = 0;
            _fragmented_nal = new ByteBuffer();
        }

        ~H264RtpDepacketizer()
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
            try
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
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                throw e;
            }
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
            var header = packet.Payload.ReadByte();
            int nal_header_f_bit = (header >> 7) & 0x01;
            int nal_header_nri = (header >> 5) & 0x03;
            int nal_header_type = (header >> 0) & 0x1F;

            if (nal_header_type >= 1 && nal_header_type <= 23)
            {
                Console.WriteLine("Normal NAL");
                _frame.WriteInt32(packet.Payload.Length);
                _frame.Write(packet.Payload);
            }
            else if (nal_header_type == 24)
            {
                // 多NALs的数据包，每个有16bit的header
                Console.WriteLine("Agg STAP-A");
                while (packet.Payload.Position < packet.Payload.Length)
                {
                    int size = packet.Payload.ReadUInt16();
                    var frame = packet.Payload.ReadSlice(size);
                    _frame.WriteInt32(frame.Length);
                    _frame.Write(frame);
                }
            }
            else if (nal_header_type == 28)
            {
                Console.WriteLine("Frag FU-A");
                // 1个NALs的数据拆分包
                var fuHeader = packet.Payload.ReadByte();
                int fu_header_s = (fuHeader >> 7) & 0x01;  // start marker
                int fu_header_e = (fuHeader >> 6) & 0x01;  // end marker
                int fu_header_r = (fuHeader >> 5) & 0x01;  // reserved. should be 0
                int fu_header_type = (fuHeader >> 0) & 0x1F; // Original NAL unit header

                Console.WriteLine("Frag FU-A s=" + fu_header_s + "e=" + fu_header_e);
                if (fu_header_s == 1 && fu_header_e == 0)
                {
                    // Start of Fragment. 开始包
                    // Initiise the fragmented_nal byte array
                    // Build the NAL header with the original F and NRI flags but use the the Type field from the fu_header_type
                    byte reconstructed_nal_type = (byte)((nal_header_f_bit << 7) + (nal_header_nri << 5) + fu_header_type);
                    _fragmented_nal = new ByteBuffer();
                    _fragmented_nal.WriteByte(reconstructed_nal_type);
                    _fragmented_nal.Write(packet.Payload, 2);
                }
                else if (fu_header_s == 0 && fu_header_e == 0)
                {
                    // Middle part of Fragment
                    // Append this payload to the fragmented_nal
                    // Data starts after the NAL Unit Type byte and the FU Header byte
                    _fragmented_nal.Write(packet.Payload, 2);
                }
                else if (fu_header_s == 0 && fu_header_e == 1)
                {
                    // End part of Fragment
                    // Append this payload to the fragmented_nal
                    // Data starts after the NAL Unit Type byte and the FU Header byte
                    _fragmented_nal.Write(packet.Payload, 2);

                    _frame.WriteInt32(_fragmented_nal.Length);
                    _frame.Write(_fragmented_nal);
                }
            }
            else
            {
                Console.WriteLine("Unknown NAL header " + nal_header_type + " not supported");
            }
        }
    }


}
